using IplStore.Api.Identity;
using IplStore.Application.Common;
using IplStore.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.ErrorHandling;

/// <summary>
/// Converts exceptions into RFC 7807 ProblemDetails with a stable machine-readable <c>code</c>.
/// Controllers never contain try/catch; the mapping below is the single source of truth.
/// Unknown exceptions become a generic 500 - internal details are logged, never returned.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = Map(exception);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogInformation("Request rejected with {Status}: {Message}", problem.Status, exception.Message);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    internal static ProblemDetails Map(Exception exception) => exception switch
    {
        RequestValidationException ex => new ValidationProblemDetails(ex.Errors.ToDictionary(e => e.Key, e => e.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = ex.Message,
            Extensions = { ["code"] = "validation.failed" },
        },
        MissingCustomerIdentityException ex => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message, "auth.missing_customer"),
        EntityNotFoundException ex => Problem(StatusCodes.Status404NotFound, "Not found", ex.Message, ex.Code),
        ConcurrencyConflictException ex => Problem(StatusCodes.Status409Conflict, "Conflict", ex.Message, "concurrency.conflict"),
        DomainException ex => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", ex.Message, ex.Code),
        BadHttpRequestException ex => Problem(ex.StatusCode, "Bad request", ex.Message, "http.bad_request"),
        OperationCanceledException => Problem(499, "Client closed request", "The request was cancelled.", "http.cancelled"),
        _ => Problem(StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred. Please retry.", "server.error"),
    };

    private static ProblemDetails Problem(int status, string title, string detail, string code) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Extensions = { ["code"] = code },
    };
}
