namespace Accounts.Domain.Models;

public sealed class GameLinking
{
    public long UserId { get; set; }
    public DateTimeOffset ConnectedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public string ConnectingHash { get; set; } = string.Empty;
    public long RowVersion { get; set; }
}
