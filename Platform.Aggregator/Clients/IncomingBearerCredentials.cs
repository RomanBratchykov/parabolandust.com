using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Platform.ServiceDefaults.Authentication;

namespace Platform.Aggregator.Clients;

public sealed class IncomingBearerCredentials(IHttpContextAccessor accessor, IConfiguration configuration,
    IdentityServerTokenProvider identityServerTokens)
{
    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = accessor.HttpContext
            ?? throw new InvalidOperationException("A validated incoming request is required for a business RPC.");
        var authentication = await context.AuthenticateAsync(PlatformAuthentication.Scheme);
        if (!authentication.Succeeded || authentication.Principal?.IsInRole("Admin") != true)
            throw new InvalidOperationException("An authenticated administrator is required for a business RPC.");

        if (configuration["Authentication:Provider"] == "IdentityServer")
            return await identityServerTokens.GetAsync(cancellationToken);
        if (configuration["Authentication:Provider"] != "IdentityJwt")
            throw new InvalidOperationException("Downstream credentials are not configured for the active provider.");

        var header = context.Request.Headers.Authorization;
        if (header.Count != 1 || !AuthenticationHeaderValue.TryParse(header[0], out var parsed) ||
            !string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parsed.Parameter))
            throw new InvalidOperationException("A bearer credential is required for a business RPC.");

        cancellationToken.ThrowIfCancellationRequested();
        return parsed.Parameter;
    }
}
