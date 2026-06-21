using GrantAI.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace GrantAI.API.Controllers;

/// <summary>
/// Maps an Application <see cref="Result{T}"/> to an MVC action result so the
/// controllers don't repeat the same if/else dance.
/// </summary>
internal static class ResultExtensions
{
    public static ActionResult<T> ToActionResult<T>(
        this Result<T> result, ControllerBase controller, ProblemDetailsFactory problemFactory)
    {
        if (result.TryGetValue(out var value, out var error))
            return controller.Ok(value);

        var status = error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = problemFactory.CreateProblemDetails(
            controller.HttpContext,
            statusCode: status,
            title: TitleFor(error.Kind),
            detail: error.Message);
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem) { StatusCode = status };
    }

    private static string TitleFor(ErrorKind kind) => kind switch
    {
        ErrorKind.NotFound => "Not found",
        ErrorKind.Validation => "Validation failed",
        _ => "Unexpected error"
    };
}
