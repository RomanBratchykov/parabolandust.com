# Lab 9 chunk 3: refresh-token persistence operations

Status: implementation only. No builds, tests, application launches or database
commands were run by the implementation model. All checks are pending for another
model. Do not treat these instructions as evidence of passing checks.

The service uses a fresh `ApplicationDbContext` per operation from
`IDbContextFactory<ApplicationDbContext>`. Provider selection, factory registration,
migrations, HTTP endpoints/cookies and Identity account checks remain pending.
No production route issues tokens yet.

## Preparation

- [ ] Restore/build Auth.Api with .NET 10 and resolve any compilation errors.
- [ ] After the user chooses the relational provider, configure a separate
      disposable Auth database and register `AddDbContextFactory<ApplicationDbContext>`.
- [ ] Apply the Identity/refresh model to that disposable database. Register
      `IJwtTokenService` and `IRefreshTokenService` in an isolated harness.
- [ ] Use a real relational database for transaction/concurrency checks; EF's
      in-memory provider is insufficient. Retain all production business data.

## Issuance and rotation

- [ ] Issue for an existing Identity user. Only a 64-character SHA-256 hex digest
      is stored; the returned raw value must differ from the persisted value.
- [ ] Check `ExpiresAt - CreatedAt` is exactly seven days and `RevokedAt` and
      `ReplacementTokenId` start null. Unknown user FK fails without an orphan.
- [ ] Rotate once: old row is revoked, replacement belongs to the same user,
      points from old to new, and has a fresh raw token/digest and seven-day expiry.
- [ ] Reusing the old value returns null without inserting another successor.
- [ ] Expired, revoked, unknown, empty, whitespace and oversized values fail.
- [ ] Race two rotations with the same old token using independent contexts:
      exactly one replacement succeeds; the losing inserted row is rolled back.
      Record actual outcomes and row counts. Check both providers if both are
      supported; report serialization/deadlock behavior rather than claiming
      every provider failure is handled as an ordinary invalid-token result.
- [ ] Force insert/update failure or cancellation before commit: old token remains
      usable and no replacement survives. Cancellation propagates to the caller.
- [ ] Rotate concurrently with revocation: revocation of the old token must
      invalidate a rotation using a stale tracked copy. Revoking an already
      consumed old token does not revoke its successor; family revocation is not
      implemented or claimed by this chunk.

## Ownership and delivery boundary

- [ ] Revoke using the token owner's validated subject: the row is revoked once.
      A different subject cannot revoke it; repeat/unknown tokens return false.
- [ ] Confirm `RefreshTokenGrant.ToString()` redacts the secret. Do not serialize
      or destructure-log the object: its Token property intentionally contains
      the raw cookie value and is not a public response DTO.
- [ ] Future callers must check confirmed email/lockout/account status before
      session issuance or access-token delivery. `IssueAsync` is an internal
      primitive and does not authenticate credentials.
- [ ] Future HTTP integration must emit refresh values only in HttpOnly cookies,
      access values in JSON, with Secure/SameSite/path and CSRF controls.

Reference: [Microsoft EF concurrency handling](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).
