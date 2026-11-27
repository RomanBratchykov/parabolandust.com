using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Contracts;

namespace Platform.Aggregator.Services;

public sealed class PlayerDashboardService(
    AccountsClient accounts, NewslettersClient newsletters, GameStatsClient statistics,
    ILogger<PlayerDashboardService> logger)
{
    private static readonly ActivitySource Activities = new("Platform.Aggregator");

    public async Task<PlayerDashboardDto?> GetDashboardAsync(long userId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        using var activity = Activities.StartActivity("GetPlayerDashboard");
        activity?.SetTag("user.id", userId);

        var account = await ComponentResult<AccountResponse>.FetchAsync(
            () => accounts.GetUserAsync(userId, cancellationToken),
            value => value.UserId == userId && !string.IsNullOrWhiteSpace(value.UserName),
            "accounts-api", logger, cancellationToken);
        if (account.Warning is not null)
        {
            if (account.Missing)
                return null;
            activity?.SetStatus(ActivityStatusCode.Error, "Accounts unavailable");
            throw new DashboardUnavailableException(account.TimedOut);
        }

        var subscriptionsTask = ComponentResult<SubscriptionsResponse>.FetchAsync(
            () => newsletters.GetSubscriptionsAsync(userId, cancellationToken),
            value => value.Id == userId && value.Subscriptions is not null &&
                value.Subscriptions.All(item => item is not null && item.Id > 0 &&
                    item.NewsletterId > 0 && !string.IsNullOrWhiteSpace(item.NewsletterTitle) &&
                    !string.IsNullOrWhiteSpace(item.Status)),
            "newsletters-api", logger, cancellationToken);
        var statisticsTask = ComponentResult<StatisticsResponse>.FetchAsync(
            () => statistics.GetStatisticsAsync(userId, cancellationToken),
            value => value.UserId == userId && value.Totals is not null,
            "gamestats-api", logger, cancellationToken);
        await Task.WhenAll(subscriptionsTask, statisticsTask);
        cancellationToken.ThrowIfCancellationRequested();

        var subscriptions = await subscriptionsTask;
        var stats = await statisticsTask;
        var warnings = new[] { subscriptions.Warning, stats.Warning }
            .OfType<string>().ToArray();
        var player = account.Value!;
        logger.LogInformation("Composed dashboard for {UserId} with {WarningCount} warnings",
            userId, warnings.Length);
        activity?.SetTag("dashboard.warning_count", warnings.Length);

        return new PlayerDashboardDto(
            new DashboardPlayerDto(player.UserId, player.UserName, player.UserPhotoUrl),
            subscriptions.Value?.Subscriptions.Select(item => new DashboardSubscriptionDto(
                item.Id, item.NewsletterId, item.NewsletterTitle, item.Status, item.ConsentAt)).ToArray(),
            stats.Value is { } value ? new DashboardStatisticsDto(value.Totals.TimesKilled,
                value.Totals.TimesWon, value.Totals.TimePlayed, value.UpdatedAt) : null,
            warnings);
    }
}
