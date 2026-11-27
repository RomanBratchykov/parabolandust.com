namespace Accounts.Bll;

using Accounts.Bll.Contracts;
using Accounts.Dal;
using Accounts.Domain.Exceptions;
using Accounts.Domain.Models;
using AutoMapper;
using Npgsql;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Accounts.Bll.Events;
using Platform.Messaging;
using Platform.Messaging.Events;

public sealed class AccountsService : IAccountsService
{
    private static readonly ActivitySource Source = new("Platform.Accounts");
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<AccountsService> _logger;
    private readonly IEventPublisher _publisher;
    private readonly IEventCorrelation _correlation;

    public AccountsService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<AccountsService> logger,
        IEventPublisher publisher, IEventCorrelation correlation)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _publisher = publisher;
        _correlation = correlation;
    }

    public async Task<UserResponse?> GetUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : _mapper.Map<UserResponse>(user);
    }

    public async Task<long> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = Source.StartActivity("CreateUser");
        if ((request.OAuthProvider is null) != (request.OAuthId is null))
            throw new ValidationException("OAuth provider and OAuth ID must be provided together.");
        var hasOAuthIdentity = request.OAuthProvider is not null;
        if ((request.PasswordHash is null && !hasOAuthIdentity) ||
            (request.PasswordHash is not null && hasOAuthIdentity))
            throw new ValidationException("Provide either a password hash or a complete OAuth identity.");

        AccountSnapshot snapshot;
        long userId;
        UserAchievement? initialUnlock = null;
        await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            if (request.InitialAchievementId is long achievementId &&
                await _unitOfWork.Achievements.GetByIdAsync(achievementId, cancellationToken) is null)
                throw new NotFoundException($"Achievement {achievementId} was not found.");

            userId = await _unitOfWork.Users.AddAsync(new AccountUser
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
                initialUnlock = new UserAchievement { UserId = userId, AchievementId = initialAchievementId };
                if (!await _unitOfWork.UserAchievements.AddAsync(initialUnlock, cancellationToken))
                    throw new BusinessConflictException("The user already has this achievement.");
            }
            snapshot = await ReadAccountSnapshotAsync(userId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw new BusinessConflictException("A user with the same unique account details already exists.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
        activity?.SetTag("user.id", userId);
        _logger.LogInformation("Account created for user {UserId}", userId);
        await PublishAccountEventAsync(new AccountCreatedEvent(snapshot) { OccurredAt = snapshot.SourceUpdatedAt });
        if (initialUnlock is not null)
            await PublishAccountEventAsync(new AchievementUnlockedEvent(userId, initialUnlock.AchievementId,
                initialUnlock.UnlockedAt) { OccurredAt = initialUnlock.UnlockedAt });
        return userId;
    }

    public async Task UpdateUserAsync(long userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        AccountSnapshot snapshot;
        await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException($"User {userId} was not found.");
            user.UserName = request.UserName;
            user.UserEmail = request.UserEmail;
            user.UserPhotoUrl = request.UserPhotoUrl;
            if (!await _unitOfWork.Users.UpdateProfileAsync(user, request.ExpectedRowVersion, cancellationToken))
                throw new BusinessConflictException("The user changed since it was loaded. Reload and retry.");
            snapshot = await ReadAccountSnapshotAsync(userId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "P0001")
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw new BusinessConflictException("The user changed since it was loaded. Reload and retry.");
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw new BusinessConflictException("The user name or email is already in use.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
        await PublishAccountEventAsync(new AccountProfileChangedEvent(snapshot) { OccurredAt = snapshot.SourceUpdatedAt });
    }

    public async Task DeleteUserAsync(long userId, long expectedRowVersion, CancellationToken cancellationToken = default)
    {
        AccountSnapshot snapshot;
        await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            if (!await _unitOfWork.Users.SoftDeleteAsync(userId, expectedRowVersion, cancellationToken))
                throw new BusinessConflictException("The user changed or was deleted. Reload and retry.");
            snapshot = await ReadAccountSnapshotAsync(userId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "P0001")
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw new BusinessConflictException("The user changed or was deleted. Reload and retry.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
        await PublishAccountEventAsync(new AccountDeletedEvent(snapshot) { OccurredAt = snapshot.SourceUpdatedAt });
    }

    public async Task RotateIdentityAsync(long userId, RotateIdentityRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserWhoAmI) || request.UserWhoAmI.Length > 128)
            throw new ValidationException("A game identity of at most 128 characters is required.");
        AccountSnapshot snapshot;
        await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            if (!await _unitOfWork.Users.RotateIdentityAsync(userId, request.UserWhoAmI, request.ExpectedRowVersion, cancellationToken))
                throw new BusinessConflictException("The user changed or was deleted. Reload and retry.");
            snapshot = await ReadAccountSnapshotAsync(userId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw new BusinessConflictException("The game identity is already in use.");
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
        await PublishAccountEventAsync(new AccountIdentityRotatedEvent(snapshot) { OccurredAt = snapshot.SourceUpdatedAt });
    }

    private async Task<AccountSnapshot> ReadAccountSnapshotAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetIncludingDeletedAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("The persisted account snapshot was not found.");
        return new AccountSnapshot(user.UserId, user.UserName, user.UserEmail, user.UserWhoAmI,
            user.CreatedAt, user.UpdatedAt ?? user.CreatedAt, user.RowVersion, user.IsDeleted);
    }

    private async Task PublishAccountEventAsync(IntegrationEvent value)
    {
        try
        {
            // The source committed; a disconnected HTTP caller must not suppress publication.
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _publisher.PublishAsync(value with { CorrelationId = _correlation.Current }, budget.Token);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Account change committed but event {EventId} publication failed", value.EventId);
            throw new AccountEventPublicationException(exception);
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
        var unlock = new UserAchievement { UserId = request.UserId, AchievementId = request.AchievementId };
        await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            if (await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken) is null)
                throw new NotFoundException($"User {request.UserId} was not found.");
            if (await _unitOfWork.Achievements.GetByIdAsync(request.AchievementId, cancellationToken) is null)
                throw new NotFoundException($"Achievement {request.AchievementId} was not found.");

            var added = await _unitOfWork.UserAchievements.AddAsync(unlock, cancellationToken);
            if (!added)
                throw new BusinessConflictException("The user already has this achievement.");
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
        await PublishAccountEventAsync(new AchievementUnlockedEvent(unlock.UserId, unlock.AchievementId,
            unlock.UnlockedAt) { OccurredAt = unlock.UnlockedAt });
    }

}
