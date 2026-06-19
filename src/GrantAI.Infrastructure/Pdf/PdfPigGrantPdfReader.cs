using GrantAI.Application.Importing.Grants;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace GrantAI.Infrastructure.Pdf;

/// <summary>
/// Reads "СПИСОК ОБЛАДАТЕЛЕЙ ОБРАЗОВАТЕЛЬНЫХ ГРАНТОВ" PDFs with UglyToad.PdfPig
/// and reconstructs reading-order lines from positioned words. Words sharing a
/// baseline (within a small Y tolerance) are grouped into a <see cref="RawGrantLine"/>
/// and sorted by X. The X coordinates are preserved alongside the joined text
/// so the pure parser can use them when the header is ambiguous about columns
/// (notably the optional ОВПО column).
///
/// PdfPig is used because it works in the Linux container without an external
/// renderer and because, unlike pdftotext, it exposes word geometry — that is
/// what lets us tell the score column apart from the ОВПО column on rows where
/// both fields are 1–3-digit integers.
/// </summary>
public sealed class PdfPigGrantPdfReader : IGrantPdfReader
{
    /// <summary>
    /// Words within this many points of each other vertically are treated as
    /// the same line. Roughly half a typical body-text line height.
    /// </summary>
    private const double LineYTolerance = 3.0;

    public RawGrantPdf Read(Stream content, string fileName)
    {
        // PdfPig prefers a seekable stream; copy if necessary so we can use it
        // a second time if a re-read is ever needed.
        using var seekable = ToSeekable(content);
        using var document = PdfDocument.Open(seekable);

        var pages = new List<RawGrantPage>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            var lines = BuildLines(page);
            pages.Add(new RawGrantPage(page.Number, lines));
        }

        return new RawGrantPdf(fileName, pages);
    }

    private static IReadOnlyList<RawGrantLine> BuildLines(Page page)
    {
        // PdfPig already groups characters into words; we just need to group
        // words into lines by Y and sort them by X.
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .ToList();
        if (words.Count == 0) return [];

        // Sort top-to-bottom (PdfPig's Y grows upwards, so descending Y first).
        var ordered = words
            .OrderByDescending(w => w.BoundingBox.Top)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var lines = new List<RawGrantLine>();
        var current = new List<Word> { ordered[0] };
        var currentTop = ordered[0].BoundingBox.Top;

        for (var i = 1; i < ordered.Count; i++)
        {
            var word = ordered[i];
            if (Math.Abs(word.BoundingBox.Top - currentTop) <= LineYTolerance)
            {
                current.Add(word);
            }
            else
            {
                lines.Add(BuildLine(current));
                current = [word];
                currentTop = word.BoundingBox.Top;
            }
        }
        lines.Add(BuildLine(current));

        return lines;
    }

    private static RawGrantLine BuildLine(List<Word> words)
    {
        var sorted = words.OrderBy(w => w.BoundingBox.Left).ToList();
        var tokens = sorted
            .Select(w => new RawGrantToken(w.BoundingBox.Left, w.Text))
            .ToList();
        var text = string.Join(' ', sorted.Select(w => w.Text));
        // Use the baseline (Top) of the first word as the line's Y; tolerance
        // grouping ensures the rest are within a couple of points anyway.
        return new RawGrantLine(sorted[0].BoundingBox.Top, text, tokens);
    }

    /// <summary>PdfPig.Open expects a seekable stream — wrap non-seekable inputs in a MemoryStream.</summary>
    private static Stream ToSeekable(Stream content)
    {
        if (content.CanSeek)
        {
            // Caller-owned stream; hand back a non-disposing wrapper so we can
            // safely `using` whatever we return without closing the input.
            return new NonDisposingStream(content);
        }

        var ms = new MemoryStream();
        content.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    private sealed class NonDisposingStream : Stream
    {
        private readonly Stream _inner;
        public NonDisposingStream(Stream inner) => _inner = inner;
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        protected override void Dispose(bool disposing) { /* never close the caller's stream */ }
    }
}
