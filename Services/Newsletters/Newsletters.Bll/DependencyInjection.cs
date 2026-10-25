using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newsletters.Bll.Services;
using Newsletters.Bll.Validation;
using Newsletters.Dal;

namespace Newsletters.Bll;

public static class DependencyInjection
{
    public static IServiceCollection AddNewslettersBll(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NewslettersDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'NewslettersDb' is required.");

        services.AddNewslettersDal(connectionString);
        services.AddAutoMapper(options =>
        {
            var licenseKey = configuration["AutoMapper:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
                options.LicenseKey = licenseKey;
            options.AddProfile<NewsletterMappingProfile>();
        });
        services.AddValidatorsFromAssemblyContaining<CreateNewsletterRequestValidator>();
        services.AddScoped<INewsletterService, NewsletterService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        return services;
    }
}
