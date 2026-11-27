using Auth.Api.Models;

namespace Auth.Api.Email;

public interface IAuthEmailSender
{
    Task SendConfirmationAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(ApplicationUser user, string token, CancellationToken cancellationToken = default);
}
