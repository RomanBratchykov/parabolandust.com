using Accounts.Bll.Contracts;
using Accounts.Domain.Models;
using AutoMapper;

namespace Accounts.Bll;

public sealed class AccountMappingProfile : Profile
{
    public AccountMappingProfile()
    {
        CreateMap<AccountUser, UserResponse>();
        CreateMap<Donation, DonationResponse>()
            .ForCtorParam(nameof(DonationResponse.Currency), options => options.MapFrom(donation => donation.Currency.Trim()));
    }
}
