using System.Text.Json;
using Auth.Api.Models;

namespace Auth.Api.Email;

public sealed class DevelopmentEmailSender : IAuthEmailSender
{
    private readonly string pickupDirectory;

    public DevelopmentEmailSender(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Development email pickup is available only in Development.");

        var path = configuration["Email:PickupDirectory"];
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("Email:PickupDirectory must be an absolute path to a private development mailbox.");

        pickupDirectory = path;
    }

    public Task SendConfirmationAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
        => WriteAsync(user, token, "verify-email", cancellationToken);

    public Task SendPasswordResetAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default)
        => WriteAsync(user, token, "reset-password", cancellationToken);

    private async Task WriteAsync(ApplicationUser user, string token, string purpose, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(pickupDirectory);
        var path = Path.Combine(pickupDirectory, $"{Guid.NewGuid():N}.json");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, bufferSize: 4096, useAsync: true);
        // These files are the developer's mailbox; security tokens never enter telemetry.
        await JsonSerializer.SerializeAsync(stream, new
        {
            UserId = user.Id,
            To = user.Email,
            Purpose = purpose,
            Token = token,
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken: cancellationToken);
    }
}
