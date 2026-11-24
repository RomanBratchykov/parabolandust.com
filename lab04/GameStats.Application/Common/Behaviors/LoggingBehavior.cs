using MediatR;
using Microsoft.Extensions.Logging;

namespace GameStats.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {Request}", name);

        try
        {
            var response = await next();
            logger.LogInformation("Handled {Request}", name);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Request {Request} failed: {ExceptionType}", name, ex.GetType().Name);
            throw;
        }
    }
}