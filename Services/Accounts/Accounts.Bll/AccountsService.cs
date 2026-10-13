namespace Accounts.Bll;

using Accounts.Bll.Contracts;
using Accounts.Dal;
using Accounts.Domain.Exceptions;
using Accounts.Domain.Models;
using AutoMapper;
using Npgsql;

public sealed class AccountsService : IAccountsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public AccountsService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<UserResponse?> GetUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : _mapper.Map<UserResponse>(user);
    }

    public async Task<long> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if ((request.OAuthProvider is null) != (request.OAuthId is null))
            throw new ValidationException("OAuth provider and OAuth ID must be provided together.");
        var hasOAuthIdentity = request.OAuthProvider is not null;
        if ((request.PasswordHash is null && !hasOAuthIdentity) ||
            (request.PasswordHash is not null && hasOAuthIdentity))
            throw new ValidationException("Provide either a password hash or a complete OAuth identity.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            if (request.InitialAchievementId is long achievementId &&
                await _unitOfWork.Achievements.GetByIdAsync(achievementId, cancellationToken) is null)
                throw new NotFoundException($"Achievement {achievementId} was not found.");

            var userId = await _unitOfWork.Users.AddAsync(new AccountUser
            {
                UserName = request.UserName,
                UserEmail = request.UserEmail,
                PasswordHash = request.PasswordHash,
                OAuthProvider = request.OAuthProvider,
                OAuthId = request.OAuthId,
                UserWhoAmI = request.UserWhoAmI,
                UserPhotoUrl = request.UserPhotoUrl
            }, cancellationToken);
            if (request.InitialAchievementId is long initialAchievementId)
            {
                await _unitOfWork.UserAchievements.AddAsync(
                    new UserAchievement { UserId = userId, AchievementId = initialAchievementId }, cancellationToken);
            }
            await _unitOfWork.CommitAsync(cancellationToken);
            return userId;
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw new BusinessConflictException("A user with the same unique account details already exists.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateUserAsync(long userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} was not found.");
        user.UserName = request.UserName;
        user.UserEmail = request.UserEmail;
        user.UserPhotoUrl = request.UserPhotoUrl;
        try
        {
            await _unitOfWork.Users.UpdateProfileAsync(user, request.ExpectedRowVersion, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "P0001")
        {
            throw new BusinessConflictException("The user changed since it was loaded. Reload and retry.");
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            throw new BusinessConflictException("The user name or email is already in use.");
        }
    }

    public async Task DeleteUserAsync(long userId, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        try
        {
            await _unitOfWork.Users.SoftDeleteAsync(userId, expectedRowVersion, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "P0001")
        {
            throw new BusinessConflictException("The user changed or was deleted. Reload and retry.");
        }
    }

    public async Task<DonationResponse?> GetDonationAsync(long donationId, CancellationToken cancellationToken = default)
    {
        var donation = await _unitOfWork.Donations.GetByIdAsync(donationId, cancellationToken);
        return donation is null ? null : _mapper.Map<DonationResponse>(donation);
    }

    public async Task<long> CreateDonationAsync(CreateDonationRequest request, CancellationToken cancellationToken = default)
    {
        var donation = new Donation
        {
            UserId = request.UserId,
            DonationAmount = request.DonationAmount,
            Commentary = request.Commentary,
            Currency = request.Currency.ToUpperInvariant(),
            IsAnonymous = request.IsAnonymous,
            ProviderPaymentId = request.ProviderPaymentId
        };
        try
        {
            return await _unitOfWork.Donations.AddAsync(donation, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            throw new BusinessConflictException("The payment provider ID has already been used.");
        }
        catch (PostgresException exception) when (exception.SqlState == "P0002")
        {
            throw new NotFoundException("The active user was not found.");
        }
    }

    public async Task UpdateDonationAsync(long donationId, UpdateDonationRequest request, CancellationToken cancellationToken = default)
    {
        var donation = await _unitOfWork.Donations.GetByIdAsync(donationId, cancellationToken)
            ?? throw new NotFoundException($"Donation {donationId} was not found.");
        donation.DonationAmount = request.DonationAmount;
        donation.Commentary = request.Commentary;
        donation.Currency = request.Currency.ToUpperInvariant();
        donation.IsAnonymous = request.IsAnonymous;
        donation.ProviderPaymentId = request.ProviderPaymentId;
        donation.RowVersion = request.ExpectedRowVersion;
        if (!await _unitOfWork.Donations.UpdateAsync(donation, cancellationToken))
            throw new BusinessConflictException("The donation changed or was deleted. Reload and retry.");
    }

    public async Task DeleteDonationAsync(long donationId, CancellationToken cancellationToken = default)
    {
        if (!await _unitOfWork.Donations.DeleteAsync(donationId, cancellationToken))
            throw new NotFoundException($"Donation {donationId} was not found.");
    }

    public async Task ChangeDonationStatusAsync(long donationId, ChangeDonationStatusRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await _unitOfWork.Donations.ChangeStatusAsync(donationId, request.Status, request.ExpectedRowVersion, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "P0001")
        {
            throw new BusinessConflictException("The donation status is invalid or the donation changed.");
        }
        catch (PostgresException exception) when (exception.SqlState == "P0002")
        {
            throw new NotFoundException($"Donation {donationId} was not found.");
        }
    }

    public async Task GrantAchievementAsync(GrantAchievementRequest request, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            if (await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken) is null)
                throw new NotFoundException($"User {request.UserId} was not found.");
            if (await _unitOfWork.Achievements.GetByIdAsync(request.AchievementId, cancellationToken) is null)
                throw new NotFoundException($"Achievement {request.AchievementId} was not found.");

            var added = await _unitOfWork.UserAchievements.AddAsync(
                new UserAchievement { UserId = request.UserId, AchievementId = request.AchievementId }, cancellationToken);
            if (!added)
                throw new BusinessConflictException("The user already has this achievement.");
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

}
