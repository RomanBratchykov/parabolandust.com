# Lab 9 chunk 2: Identity persistence model

Status: code written, unverified. No builds, tests, app launches, database commands,
or migrations were run by the implementation model. All checks below are pending
and assigned to another model.

This chunk adds the provider-independent `ApplicationDbContext` and `RefreshToken`
entity, using Identity EF Core 10.0.4 to match the existing .NET 10 / EF 10.0.4
stack. It does not register a database provider, create tables, or implement token
storage operations. Existing token-service behavior remains the chunk 1 scope.

## Code and model checks

- [ ] Restore/build Auth.Api and confirm package compatibility.
- [ ] Configure this context in an isolated harness using the chosen relational
      provider. Confirm all standard Identity entities remain in its model;
      `base.OnModelCreating` must retain their keys, indexes and relationships.
- [ ] Inspect `RefreshTokens`: Guid primary key; required user FK, hash, creation
      and expiration timestamps; optional revocation and replacement metadata.
- [ ] Verify unique token-hash index and the user/expiration lookup index.
- [ ] Verify user FK enforcement and intended cascade behavior on user deletion.
- [ ] Verify replacement FK enforcement, single-successor relationship and delete
      restriction. Check user deletion after building a rotated-token chain;
      report provider-specific cascade/restrict conflicts before wiring deletes.
- [ ] Load a token in two separate contexts, revoke/save in the first, then
      revoke/save the stale copy. The second save must raise
      `DbUpdateConcurrencyException` because `RevokedAt` changed.
- [ ] Ensure there is no raw refresh-token property and no test credential or
      connection string in the code. Future writes must compute SHA-256 hex
      digests; `HasMaxLength(64)` alone does not validate their content.

## Pending integration (not implemented by this chunk)

- [ ] Resolve the source Lab 9 SQL Server requirement versus the project
      PostgreSQL rule with the user before selecting a provider.
- [ ] Register the provider, Identity stores/options and explicit bearer scheme;
      configure separate IdentityDb and persistent Data Protection keys.
- [ ] Generate migrations with the chosen provider and inspect/apply them only
      to the separate Auth store; preserve existing business databases.
- [ ] Implement seven-day refresh issuance, hashing, transactional rotation and
      revocation. Handle concurrency failure as a rejected refresh, with the
      attempted replacement rolled back. The concurrency mapping alone does
      not implement atomic rotation or prevent expiration/replay.
- [ ] Implement repeatable User/Admin seeding, confirmed-email login, password
      minimum length 8 and lockout after five failures.

Reference: [Microsoft Identity model customization](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model).
