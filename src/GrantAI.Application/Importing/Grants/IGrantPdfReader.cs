namespace GrantAI.Application.Importing.Grants;

/// <summary>
/// Reads a "СПИСОК ОБЛАДАТЕЛЕЙ ОБРАЗОВАТЕЛЬНЫХ ГРАНТОВ" PDF into a layout-
/// agnostic shape: pages of <see cref="RawGrantLine"/>s, each a list of
/// horizontally ordered tokens with their X coordinate. The concrete reader
/// (UglyToad.PdfPig) lives in Infrastructure; keeping the port here means the
/// import use-case and its tests never depend on a PDF library.
///
/// Tokens carry an X coordinate so the parser can tell columns apart when the
/// header is ambiguous — in particular, whether a block has an ОВПО column.
/// </summary>
public interface IGrantPdfReader
{
    RawGrantPdf Read(Stream content, string fileName);
}

/// <summary>A PDF reduced to its pages and reconstructed text lines.</summary>
public sealed record RawGrantPdf(string FileName, IReadOnlyList<RawGrantPage> Pages);

/// <summary>One page: 1-based page number and its lines top-to-bottom.</summary>
public sealed record RawGrantPage(int PageNumber, IReadOnlyList<RawGrantLine> Lines);

/// <summary>
/// One reconstructed line of text. <see cref="Tokens"/> are sorted left-to-right
/// by X coordinate; <see cref="Text"/> is the same tokens joined by spaces, kept
/// alongside for cheap substring/regex matching.
/// </summary>
public sealed record RawGrantLine(double Y, string Text, IReadOnlyList<RawGrantToken> Tokens);

/// <summary>One token (a single word) on a line, with its horizontal position.</summary>
public sealed record RawGrantToken(double X, string Text);
