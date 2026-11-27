using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newsletters.Api.Controllers;
using Newsletters.Bll.Contracts;
using Newsletters.Bll.Services;
using Newsletters.Bll.Validation;
using Newsletters.Domain.Exceptions;
using Platform.ServiceDefaults;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterBusinessMetricsTests
{
    [Fact]
    public async Task Publishing_counts_success_once_and_records_failed_attempt_duration()
    {
        var completed = 0L;
        var outcomes = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, source) =>
        {
            if (instrument.Meter.Name == "Newsletters.Api") source.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
        {
            if (instrument.Name == "newsletters.operations.completed") completed += value;
        });
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) => outcomes.Add((string)tags[1].Value!));
        listener.Start();
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddSingleton(provider => new BusinessMetrics(provider.GetRequiredService<IMeterFactory>(), "Newsletters.Api", "newsletters"));
        var service = new PublishingService();
        services.AddSingleton<INewsletterService>(service);
        using var provider = services.BuildServiceProvider();
        var controller = ActivatorUtilities.CreateInstance<NewslettersController>(provider);
        var request = new PublishNewsletterRequest("editor");
        var validator = new PublishNewsletterRequestValidator();

        Assert.IsType<NoContentResult>(await controller.Publish(1, request, validator, CancellationToken.None));
        service.Fail = true;
        await Assert.ThrowsAsync<ConflictException>(() => controller.Publish(1, request, validator, CancellationToken.None));

        Assert.Equal(1, completed);
        Assert.Equal(new[] { "success", "failure" }, outcomes);
    }

    private sealed class PublishingService : INewsletterService
    {
        public bool Fail { get; set; }
        public Task PublishAsync(long id, PublishNewsletterRequest request, CancellationToken cancellationToken = default) =>
            Fail ? Task.FromException(new ConflictException("Already published.")) : Task.CompletedTask;
        public Task<PagedResult<NewsletterDto>> GetListAsync(NewsletterQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<NewsletterDto> GetByIdAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<NewsletterDetailsDto> GetWithDetailsAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<NewsletterDto> CreateAsync(CreateNewsletterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserSubscriptionsDto> GetUserSubscriptionsAsync(long userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SubscriberDto>> GetActiveSubscribersAsync(long newsletterId, DateTimeOffset since, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
