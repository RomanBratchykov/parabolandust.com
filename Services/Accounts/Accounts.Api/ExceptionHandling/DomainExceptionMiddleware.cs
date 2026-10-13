using Accounts.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Accounts.Api.ExceptionHandling;

public sealed class DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var (status, title, detail) = exception switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
                BusinessConflictException => (StatusCodes.Status409Conflict, "Business rule conflict", exception.Message),
                ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "The request could not be completed.")
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", context.TraceIdentifier);
            }
            else
            {
                logger.LogInformation("Request rejected with {StatusCode}: {Message}", status, exception.Message);
            }

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}
