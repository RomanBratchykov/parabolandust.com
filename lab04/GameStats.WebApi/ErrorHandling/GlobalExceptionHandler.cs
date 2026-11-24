using FluentValidation;
using GameStats.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace GameStats.WebApi.ErrorHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment env,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violated"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            MongoWriteException { WriteError.Category: ServerErrorCategory.DuplicateKey }
                => (StatusCodes.Status409Conflict, "Duplicate key"),
            MongoWriteException => (StatusCodes.Status400BadRequest, "Write failed"),
            MongoConnectionException => (StatusCodes.Status503ServiceUnavailable, "Database unavailable"),
            TimeoutException => (StatusCodes.Status503ServiceUnavailable, "Database unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };

        if (status >= 500)
            logger.LogError(ex, "Request {Path} failed with {Status}", http.Request.Path, status);
        else
            logger.LogWarning("Request {Path} rejected with {Status}: {Message}",
                http.Request.Path, status, ex.Message);

        var problem = new ProblemDetails { Status = status, Title = title };

        if (ex is ValidationException validation)
        {
      
            problem.Extensions["errors"] = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }
        else if (status < 500 || env.IsDevelopment())
        {
            problem.Detail = ex.Message;
        }

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = ex
        });
    }
}