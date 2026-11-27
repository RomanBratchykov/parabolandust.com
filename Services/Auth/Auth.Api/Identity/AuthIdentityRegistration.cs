using Auth.Api.Data;
using Auth.Api.Models;
using Auth.Api.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Identity;

public static class AuthIdentityRegistration
{
    public const string BearerScheme = "IdentityJwt";

    public static IServiceCollection AddAuthIdentity(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configureDatabase)
    {
        services.AddDbContextFactory<ApplicationDbContext>(configureDatabase);
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.SignIn.RequireConfirmedEmail = true;
            options.User.RequireUniqueEmail = true;
            options.ClaimsIdentity.UserIdClaimType = "sub";
            options.ClaimsIdentity.UserNameClaimType = "unique_name";
            options.ClaimsIdentity.RoleClaimType = "role";
        })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1));
        // Identity registers cookies; API authentication must still use bearer challenges.
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = BearerScheme;
            options.DefaultChallengeScheme = BearerScheme;
            options.DefaultForbidScheme = BearerScheme;
        });
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        return services;
    }
}
