using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Newsletters.Bll;
using Xunit;

namespace Newsletters.Tests;

public sealed class NewsletterMappingTests
{
    [Fact]
    public void Mapping_profile_has_valid_maps_for_API_contracts()
    {
        var configuration = new MapperConfiguration(
            options => options.AddProfile<NewsletterMappingProfile>(), NullLoggerFactory.Instance);

        configuration.AssertConfigurationIsValid();
    }
}
