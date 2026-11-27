using System.Text.Json;
using Polly.CircuitBreaker;

namespace Platform.Aggregator.Services;

internal sealed record ComponentResult<T>(T? Value, string? Warning, bool TimedOut = false,
    bool Missing = false)
    where T : class
{
    public static async Task<ComponentResult<T>> FetchAsync(
        Func<Task<T?>> call, Func<T, bool> isValid, string service,
        ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var value = await call();
            cancellationToken.ThrowIfCancellationRequested();
            if (value is null)
            {
                logger.LogWarning("Dashboard component {Service} has no player data", service);
                return new(null, $"{service}: data not found.", Missing: true);
            }
            if (!isValid(value))
                throw new InvalidDataException("The response is invalid or belongs to another player.");

            return new(value, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException
            or InvalidDataException or JsonException or TimeoutException or BrokenCircuitException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning(exception, "Dashboard component {Service} is unavailable", service);
            return new(null, $"{service}: data unavailable.", exception is TimeoutException);
        }
    }
}

public sealed class DashboardUnavailableException(bool timedOut) : Exception("Accounts service is unavailable.")
{
    public bool TimedOut { get; } = timedOut;
}
