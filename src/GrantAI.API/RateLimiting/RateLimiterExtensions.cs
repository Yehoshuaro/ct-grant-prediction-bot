using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace GrantAI.API.RateLimiting;

/// <summary>
/// Wires up ASP.NET Core's built-in rate limiter with two policies:
///   * the global limiter — fixed window per client IP, applied to every request;
///   * the "strict" policy — a tighter window for expensive endpoints like
///     <c>POST /api/import</c> and the forecast/chance reads.
/// On rejection the response is RFC 7807 ProblemDetails with a Retry-After header.
/// </summary>
public static class RateLimiterExtensions
{
    public const string StrictPolicy = "strict";

    public static IServiceCollection AddGrantAiRateLimiter(
        this IServiceCollection services, RateLimitSettings settings)
    {
        services.AddSingleton(settings);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteProblemDetailsAsync;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (IsHealthEndpoint(context)) return RateLimitPartition.GetNoLimiter("health");

                var key = ClientKey(context);
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.Global.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.Global.WindowSeconds),
                    QueueLimit = settings.Global.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy(StrictPolicy, context =>
            {
                var key = ClientKey(context);
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.Strict.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.Strict.WindowSeconds),
                    QueueLimit = settings.Strict.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
            });
        });

        return services;
    }

    private static bool IsHealthEndpoint(HttpContext context)
        => context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    private static string ClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        return ResolveClientKey(forwarded, context.Connection.RemoteIpAddress?.ToString());
    }

    /// <summary>
    /// Picks the client identifier used to partition the rate limiter.
    /// <c>X-Forwarded-For</c> wins when present (single trusted reverse proxy
    /// in production); otherwise the connection IP, otherwise "unknown" so the
    /// limiter still has a key and anonymous traffic is still capped.
    /// </summary>
    public static string ResolveClientKey(string? forwardedFor, string? remoteIp)
    {
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var comma = forwardedFor.IndexOf(',');
            var first = (comma < 0 ? forwardedFor : forwardedFor[..comma]).Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }
        return string.IsNullOrWhiteSpace(remoteIp) ? "unknown" : remoteIp;
    }

    private static ValueTask WriteProblemDetailsAsync(OnRejectedContext context, CancellationToken ct)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/problem+json";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            response.Headers.RetryAfter = seconds.ToString();
        }

        var problem =
            "{" +
              "\"type\":\"https://datatracker.ietf.org/doc/html/rfc6585#section-4\"," +
              "\"title\":\"Too Many Requests\"," +
              "\"status\":429," +
              "\"detail\":\"Request rate limit exceeded. Please retry shortly.\"" +
            "}";

        return new ValueTask(response.WriteAsync(problem, ct));
    }
}
