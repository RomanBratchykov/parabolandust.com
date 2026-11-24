# Lab 9 chunk 4: Identity configuration and role initialization

Status: code written, unverified. The implementation model ran no builds, tests,
application, or database commands. Every check below is pending for another model.

This chunk adds `AddAuthIdentity(configureDatabase)` and `IdentityRoleInitializer`.
Auth now names its JWT handler `IdentityJwt`. Provider selection is still pending:
the registration helper and initializer are not invoked by Program yet. There is
no new database, migration, user provisioning or public endpoint in this chunk.

## Integration setup

- [ ] Restore/build Auth.Api with .NET 10.
- [ ] In an isolated harness, call `AddAuthIdentity` with the chosen relational
      provider and a disposable Identity database. Resolve UserManager,
      SignInManager, RoleManager, ApplicationDbContext, its factory, and
      IRefreshTokenService. Do not run against business databases.
- [ ] Confirm the context factory is compatible with the scoped Identity stores
      and the refresh service can create independent contexts.
- [ ] After migrations in the harness, call IdentityRoleInitializer with the
      scoped RoleManager. Startup wiring must perform this after migrations.

## Identity policy

- [ ] Reject passwords shorter than eight characters and passwords missing a
      digit, lowercase, uppercase or non-alphanumeric character; accept a valid
      eight-character password for a well-formed new user.
- [ ] Reject a duplicate email through UserManager validation. Confirm concurrent
      registration behavior separately; RequireUniqueEmail does not add a unique
      database email index or prove race safety.
- [ ] With `CheckPasswordSignInAsync(..., lockoutOnFailure: true)`, five failed
      attempts lock the user for five minutes; a correct password cannot bypass
      lockout. Validate the caller actually sets lockoutOnFailure.
- [ ] Unconfirmed email fails SignInManager checks. UserManager password checks
      alone do not enforce this: future login/refresh callers must enforce it.
- [ ] Verify Identity principals and JWT principals use `sub`, `unique_name` and
      `role` consistently; role spelling is User/Admin.
- [ ] Confirmation/reset tokens use a one-hour lifetime. Test expired and reused
      tokens when endpoints exist; configuration does not itself enforce one-use
      confirmation semantics. Persistent Data Protection keys remain pending.

## Roles and authentication

- [ ] Initialize roles twice and after restart: exactly User/Admin exist, with no
      duplicate rows and no automatic user promotion or bootstrap password.
- [ ] Run two independent initializers concurrently; both complete or report the
      actual provider failure. The final role rows must be unique.
- [ ] Force a genuine role-store failure and confirm initialization fails visibly
      without reporting successful startup or logging a credential.
- [ ] Inspect authentication options after registration: authenticate, challenge
      and forbid use IdentityJwt even if Identity's cookie defaults were set.
- [ ] Bearer-protected harness routes return 401 for missing/invalid JWT and 403
      for insufficient role, with no cookie-login redirect. An Identity cookie
      by itself must not authorize an API request.
- [ ] Confirm the existing standalone Auth token foundation still starts when
      configured with JWT settings; uninvoked Identity registration must not
      introduce a database requirement yet.

Reference: [Microsoft Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0).

Next integration requires the outstanding SQL Server/PostgreSQL choice, followed
by provider registration, migrations, persistent Data Protection and AppHost.
