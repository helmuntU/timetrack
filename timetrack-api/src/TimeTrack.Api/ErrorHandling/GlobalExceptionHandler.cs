using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Application.Common.Exceptions;
using TimeTrack.Domain.Common;

namespace TimeTrack.Api.ErrorHandling;

/// <summary>
/// One place that turns exceptions into RFC 7807 ProblemDetails responses.
/// Controllers stay clean: no try/catch, no status-code logic.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = MapKnownException(exception);

        if (problem is null)
        {
            logger.LogError(
                exception,
                "GlobalExceptionHandler.TryHandleAsync: unhandled exception on {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            // Never leak stack traces or internal messages to the client.
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            };
        }
        else
        {
            logger.LogInformation(
                "GlobalExceptionHandler.TryHandleAsync: {ExceptionType} mapped to {StatusCode} on {Method} {Path}",
                exception.GetType().Name,
                problem.Status,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails? MapKnownException(Exception exception) => exception switch
    {
        ValidationException validation => new HttpValidationProblemDetails(GroupErrors(validation))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
        },
        DomainException domain => WithCode(
            new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "A business rule was violated.",
                Detail = domain.Message,
            },
            domain.Code),
        NotFoundException notFound => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource not found.",
            Detail = notFound.Message,
        },
        ForbiddenAccessException forbidden => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden.",
            Detail = forbidden.Message,
        },
        ConcurrencyConflictException conflict => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Concurrency conflict.",
            Detail = conflict.Message,
        },
        InvalidCredentialsException invalidCredentials => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication failed.",
            Detail = invalidCredentials.Message,
        },
        UnauthorizedAccessException => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication required.",
        },
        _ => null,
    };

    private static ProblemDetails WithCode(ProblemDetails problem, string code)
    {
        problem.Extensions["code"] = code;
        return problem;
    }

    private static Dictionary<string, string[]> GroupErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

    // The Angular client uses camelCase field names, so errors map straight to form controls.
    private static string ToCamelCase(string propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? propertyName
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}
