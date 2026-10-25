using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newsletters.Domain.Exceptions;
using Npgsql;

namespace Newsletters.Api.ExceptionHandling;

public sealed class NewsletterExceptionHandler(
    ILogger<NewsletterExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        var status = GetStatusCode(exception);
        logger.LogError(exception, "Request {RequestPath} failed with status {StatusCode}",
            httpContext.Request.Path, status);

        ProblemDetails problem = exception switch
        {
            FluentValidation.ValidationException validation => CreateValidationProblem(validation),
            System.ComponentModel.DataAnnotations.ValidationException validation => new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["request"] = [validation.Message] }),
            _ => new ProblemDetails
            {
                Status = status,
                Title = GetTitle(status),
                Detail = status == StatusCodes.Status500InternalServerError && !environment.IsDevelopment()
                    ? "An unexpected error occurred."
                    : exception.Message,
                Instance = httpContext.Request.Path
            }
        };
        problem.Status = status;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, options: null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        EntityNotFoundException => StatusCodes.Status404NotFound,
        ConflictException or DomainException => StatusCodes.Status409Conflict,
        FluentValidation.ValidationException or System.ComponentModel.DataAnnotations.ValidationException or ArgumentException
            => StatusCodes.Status400BadRequest,
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
            => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    private static ValidationProblemDetails CreateValidationProblem(FluentValidation.ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
    }

    private static string GetTitle(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Request validation failed.",
        StatusCodes.Status404NotFound => "Resource not found.",
        StatusCodes.Status409Conflict => "Request conflicts with the current resource state.",
        _ => "An unexpected error occurred."
    };
}
