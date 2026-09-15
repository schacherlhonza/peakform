using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TrainCoach.Application.Auth;
using TrainCoach.Application.Common;

namespace TrainCoach.Api.Middleware;

/// <summary>
/// Maps Application-layer exceptions to RFC 7807 ProblemDetails responses with the right HTTP
/// status, so every module can just throw a typed exception instead of building responses.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Neplatný požadavek"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Přihlášení se nezdařilo"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Přístup odepřen"),
            NotFoundException => (StatusCodes.Status404NotFound, "Nenalezeno"),
            BusinessRuleException => (StatusCodes.Status409Conflict, "Neplatná operace"),
            _ => (StatusCodes.Status500InternalServerError, "Neočekávaná chyba serveru"),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Neošetřená výjimka při zpracování {Path}", httpContext.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError ? "Došlo k neočekávané chybě." : exception.Message,
            Instance = httpContext.Request.Path,
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
