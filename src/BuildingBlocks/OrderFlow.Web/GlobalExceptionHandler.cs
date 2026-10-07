using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrderFlow.SharedKernel;

namespace OrderFlow.Web;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => BuildValidationProblem(validation),
            NotFoundException => BuildProblem(StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            DomainException => BuildProblem(StatusCodes.Status409Conflict, "Business rule violated", exception.Message),
            UnauthorizedException => BuildProblem(StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
            DependencyUnavailableException => BuildProblem(StatusCodes.Status503ServiceUnavailable, "Dependency unavailable", exception.Message),
            ConflictException => BuildProblem(StatusCodes.Status409Conflict, "Conflict", exception.Message),
            _ => BuildProblem(StatusCodes.Status500InternalServerError, "Unexpected error", "Something went wrong. Use the trace id to find the log entry.")
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);
        }

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails BuildProblem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ValidationProblemDetails BuildValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed"
        };
    }
}
