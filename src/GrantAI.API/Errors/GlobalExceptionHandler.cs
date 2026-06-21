using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace GrantAI.API.Errors;

/// <summary>
/// Catch-all error handler. Translates a small set of well-known exception
/// types into specific status codes, and everything else into 500. Responses
/// are RFC 7807 ProblemDetails. Stack traces never leak to the client.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ProblemDetailsFactory problemDetailsFactory,
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsFactory = problemDetailsFactory;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case ValidationException validation:
                _logger.LogInformation(validation, "Validation failure handled");
                problem = _problemDetailsFactory.CreateProblemDetails(
                    httpContext,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation failed",
                    detail: validation.Message);
                problem.Extensions["errors"] = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => string.IsNullOrEmpty(g.Key) ? "_" : g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray());
                break;

            case BadHttpRequestException badRequest:
                problem = _problemDetailsFactory.CreateProblemDetails(
                    httpContext,
                    statusCode: badRequest.StatusCode is > 0 ? badRequest.StatusCode : StatusCodes.Status400BadRequest,
                    title: "Bad request",
                    detail: badRequest.Message);
                break;

            case OperationCanceledException:
                // The client disappeared. Use 499 (NGINX convention for client-closed
                // request); it never leaves middleware so the body content is moot.
                _logger.LogDebug("Request cancelled by the client");
                httpContext.Response.StatusCode = 499;
                return true;

            default:
                _logger.LogError(exception, "Unhandled exception");
                problem = _problemDetailsFactory.CreateProblemDetails(
                    httpContext,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal server error",
                    detail: _environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.");
                break;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
