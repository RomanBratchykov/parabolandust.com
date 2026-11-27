using AutoMapper;
using Newsletters.Bll.Contracts;
using Newsletters.Domain.Models;

namespace Newsletters.Bll;

public sealed class NewsletterMappingProfile : Profile
{
    public NewsletterMappingProfile()
    {
        CreateMap<Newsletter, NewsletterDto>();
        CreateMap<Newsletter, NewsletterDetailsDto>()
            .ForCtorParam(nameof(NewsletterDetailsDto.Newsletter),
                options => options.MapFrom(newsletter => newsletter));
        CreateMap<NewsletterDeliverySettings, NewsletterDeliverySettingsDto>();
        CreateMap<UserComment, UserCommentDto>()
            .ForCtorParam(nameof(UserCommentDto.UserName),
                options => options.MapFrom(comment => comment.User.UserName));
        CreateMap<Subscription, SubscriptionDto>()
            .ForMember(dto => dto.Status, options => options.MapFrom(subscription => subscription.Status.ToString()));
        CreateMap<Subscription, UserSubscriptionDto>()
            .ForMember(dto => dto.NewsletterTitle, options => options.MapFrom(subscription => subscription.Newsletter.Title))
            .ForMember(dto => dto.Status, options => options.MapFrom(subscription => subscription.Status.ToString()));
        CreateMap<UserCopy, UserSubscriptionsDto>();
        CreateMap<UserCopy, SubscriberDto>();
    }
}
