# Lab 9 chunk 1: token foundation

Status: implemented but unverified. The implementation model ran no build, tests,
application, or HTTP checks. A different model should execute this checklist and
record actual results; every box is pending.

Scope: `Services/Auth/Auth.Api` and its solution entry. This chunk implements token
generation/validation and bearer registration only. It has no Identity database,
registration/login routes, refresh persistence/rotation/cookies, Gateway route,
or AppHost resource. Do not report P1 or Lab 9 complete.

## Build and startup

- [ ] Restore and build `Services/Auth/Auth.Api/Auth.Api.csproj` with .NET 10.
- [ ] Confirm its solution entry resolves and existing projects still build.
- [ ] Supply `Jwt__Issuer`, `Jwt__Audience`, and a newly generated random secret
      of at least 32 UTF-8 bytes via environment or user secrets; never commit it.
- [ ] Start Auth standalone; verify health endpoints remain available.
- [ ] Missing issuer/audience/secret and a short secret must fail startup without
      printing the secret. No default signing credential should exist.

## Token service (isolated harness, no production token-issuing endpoint)

- [ ] Resolve `IJwtTokenService`; generate a token for an `ApplicationUser` with
      a string ID, username, and `User`/`Admin` roles.
- [ ] Inspect locally: HS256 signature, exact issuer/audience, `sub`, `unique_name`,
      unique `jti`, numeric `iat`, `nbf`, and `exp - iat == 900` seconds.
- [ ] Verify case-sensitive roles and duplicate-role removal; no password,
      security stamp, email confirmation/reset token, or signing secret in claims.
- [ ] Valid token returns an authenticated principal with the original subject,
      username in `Identity.Name`, and working `IsInRole("User")`/`IsInRole("Admin")`.
- [ ] Alter signature, issuer, audience and algorithm independently; each fails.
      Reject unsigned, expired, future-not-before and missing-expiry tokens.
- [ ] Null/empty/whitespace, malformed and oversized token input returns null.
- [ ] Missing user ID or username fails token generation.
- [ ] Generate multiple refresh values; each decodes to 64 random bytes and
      values differ. Generation alone must not be claimed to persist a session
      or implement the required seven-day expiration/rotation.
- [ ] In an isolated harness, bearer-protected endpoints return 401 for absent
      or invalid tokens, accept valid tokens, and enforce roles with 403.
      Ensure no cookie-login redirect occurs. Do not add a public issuance route.

Reference: [Microsoft JWT validation parameters](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters).

Next chunk: resolve the SQL Server/PostgreSQL requirement conflict before adding
the Identity store, refresh-token persistence, migrations and AppHost wiring.
