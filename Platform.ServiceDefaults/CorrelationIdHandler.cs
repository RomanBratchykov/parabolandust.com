using Microsoft.AspNetCore.Http;

namespace Platform.ServiceDefaults;

public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = accessor.HttpContext?.Request
            .Headers[CorrelationIdMiddleware.HeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            // Replace any previous value so retries and reused clients never duplicate it.
            request.Headers.Remove(CorrelationIdMiddleware.HeaderName);
            request.Headers.TryAddWithoutValidation(
                CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
