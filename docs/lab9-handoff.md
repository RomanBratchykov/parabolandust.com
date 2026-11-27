# Lab 9 unfinished-work handoff

Updated: 2026-11-25. Workspace: `D:\parabolandust.com`, branch `lab8`.

## User instructions

- Complete Lab 9 in understandable chunks and distribute independent work among
  agents. Delegation was explicitly authorized in this session.
- Initial instructions prohibited testing; the user later explicitly authorized
  tests. Builds and focused verification are now allowed.
- Write runtime checklists for other models and distinguish pending checks from
  actual results. Do not claim the lab complete from successful builds.
- New commit messages must be `[n] description`, without `feat(...)` prefixes.
  Do not rewrite older history again without a new explicit request.
- The user requested this handoff and committing the currently uncommitted files.

## Source requirements

Read the local ignored files `tasks/AGENTS.MD`, `tasks/Lab9/lab9.md`,
`tasks/Lab9/plan.md`, and `tasks/Lab9/verification.md`. The plan contains packets
P0–P9 and the verification file lists 18 Auth routes and the 13-endpoint permission
matrix. Do not edit these source requirements or weaken their checks.

The implementation uses PostgreSQL for Auth and Duende, following the repository
database rule. This adapts the assignment's SQL Server wording. The user did not
explicitly select a provider; the agent proceeded with the repository rule after
repeated instructions to continue. Grading acceptance of this adaptation remains
unconfirmed. Business databases remain separate from the security stores.

## Implemented foundations

- `Services/Auth/Auth.Api`: Identity user/context, PostgreSQL initial migration,
  password/lockout/confirmed-email settings, repeatable User/Admin roles, optional
  development Admin bootstrap, JWT signing/validation, secure refresh cookies,
  hashed refresh persistence and rotation, security-stamp binding, antiforgery,
  persistent Data Protection keys and development email pickup.
- All 18 planned Auth management/session handlers are mapped. They have built,
  but the full database-backed HTTP flow has not been verified.
- Gateway forwards `/auth/**` and `/users/**` to Auth while preserving Accounts
  `/api/users/**`. Business proxy routes require a validated bearer. Spoofed
  `X-User-Id` is stripped. HTTPS Gateway port 5001 supplements HTTP port 5000.
- Accounts, Newsletters, GameStats and Aggregator independently validate tokens.
  Their business REST/gRPC endpoints use an **interim Admin-only policy**.
  Health endpoints remain separate. Normal-user owner authorization is unfinished.
- Aggregator forwards validated incoming IdentityJwt Admin credentials per gRPC
  call. The later OAuth chunk adds bounded client-credentials acquisition and
  caching in IdentityServer mode. Health calls remain anonymous.
- `Platform.IdentityServer`: Duende 8.0.10, official attributed ASP.NET Identity
  Quickstart UI, PostgreSQL Identity/configuration/operational stores, migrations
  for three contexts, clients/scopes and optional development demo user.
- `infra/keycloak`: Pulumi realm, resource servers, 13 scope permissions, policies,
  roles, clients, demo users, audience mappers and secret outputs. Not applied.
- `Services/Payments/Payments.Api`: three permission-protected demonstration
  endpoints, Keycloak JWT validation and fail-closed permission parsing.
- AppHost includes Auth, Duende, Keycloak and Payments. It selects one business
  provider, with configurable external authority/audience. All modes are not yet
  functionally integrated.

## Latest prepared changes to review first

These were uncommitted when this handoff was requested and are being included in
the requested source/documentation commits:

1. `Services/Auth/Auth.Api/Tokens/JwtTokenService.cs`: requires a confirmed email
   and adds signed `email` / `email_verified` claims for trusted provisioning.
2. `Services/Accounts/Accounts.Dal/Repositories/IdentityLinkRepository.cs`,
   `Accounts.Bll/IdentityLinkService.cs`, `Accounts.Api/Identity/IdentityLinkEndpoints.cs`
   and Accounts Program registration: resolve/provision exact issuer/subject
   links through `/internal/identity/self` in IdentityJwt mode. Provisioning takes
   identity/profile data only from validated claims, creates a managed account
   and link transactionally, and never matches existing users by email.
3. `Platform.Aggregator/Clients/IdentityServerTokenProvider.cs`,
   `IncomingBearerCredentials.cs`, and `GrpcClientRegistration.cs`: OAuth token
   acquisition/cache for IdentityServer. Existing Admin-only backend policies
   reject service tokens until proper read-scope policies are implemented.

Accounts link storage was committed earlier in `db/p1/schema.sql` and the explicit
upgrade `db/p1/migrations/20261125_identity_links.sql`. Existing database volumes
are not automatically upgraded by the initializer. **No SQL upgrade was run.**
Managed users must have no local password/OAuth credentials. Revoked identity
pairs remain reserved; resolver denies revoked/deleted links. Provisioning's
event publication is after commit; retries republish, but there is no durable
outbox and a crash without retry can leave projections missing.

## Work still required

1. Verify and finish account provisioning, caller identity resolution and owner
   checks in every business REST/gRPC route **before caches or business work**.
   Resource-ID-only donation/subscription operations need a server-side owner
   lookup. Keep legacy account creation privileged. Do not cast string `sub` to
   numeric Accounts ID or trust request identity headers/body fields.
2. Replace InterimAdmin with complete provider-specific policies: Identity
   owner/admin, Duende read/write/service scopes, Keycloak exact resource/scope
   grants. Preserve the distinction between human ownership and M2M clients.
3. Complete the domain mapping for the assignment's Orders/Products matrix.
   Required approve/update/delete/publish surfaces do not all exist. Only the
   Payments demonstration currently has the canonical permission annotations.
4. Reconcile per-API audiences/scopes, the Gateway audience and Payments' separate
   `payments-api` audience. Configure Aggregator service credentials in AppHost.
   Wire actual Swagger Code+PKCE and verify Postman/browser flows. Duende seeds
   currently insert missing clients only; updating settings does not rotate them.
5. Finish Keycloak composite roles and a supported policy-derived permission-token
   flow. Audience/role mappers do not create `authorization.permissions` by
   themselves. Multi-resource UMA/RPT issuance/assembly remains unfinished.
6. Implement external network isolation for business APIs while keeping IdP
   discovery/login, admin provisioning and health reachable as intended. Generated
   Aspire backend ports are not evidence of isolation.
7. Review readiness, production email delivery, Data Protection protection at rest
   outside Windows, signing-key persistence, rate limits, account-enumeration
   behavior and delivery failure semantics. Sender failures can reveal matching
   accounts; mail/HTTP delivery is not atomic with database commits.
8. Apply migrations only to isolated intended databases; run real transaction,
   reset/rotation races, ownership/cache isolation and provider switching checks.
   Then rerun regression tests with authentication-aware fixtures.
9. Finish Postman collection/environment, README for all three modes, defense
   notes, redacted token samples, screenshots and the actual 52-cell role matrix.

Estimated remaining work: roughly 12–18 substantial chunks including integration,
verification/fixes and submission evidence. This is a planning estimate, not a
completion percentage. No major acceptance packet has full live evidence yet.

## Verification actually performed

- At this save, `dotnet build Platform.slnx --no-restore -m:1 --verbosity quiet`
  succeeded with zero errors and 22 existing warnings. `git diff --check` found
  no whitespace errors. Tests were not rerun during this save/commit operation.
- Auth, Accounts, Aggregator, Gateway, Payments, Duende and integrated AppHost
  builds have succeeded at relevant checkpoints. Builds are not runtime proof.
- An earlier full existing suite passed **176 tests, skipped 25, failed 0**.
  This was **before** the later backend authorization changes. It must not be
  represented as the current final regression result.
- A temporary JWT/cookie probe passed, including valid claims, rejection of wrong
  issuer/audience/key, refresh randomness, JSON redaction, cookie attributes and,
  after the latest claim change, confirmed-email claims/unconfirmed-user rejection.
- A Gateway probe confirmed 401 for missing/invalid business credentials and
  200 for liveness. Backend ownership and valid end-to-end calls were not proven.
- No live PostgreSQL migration/concurrency flow, Aspire orchestration, Duende
  OAuth browser flow, Pulumi preview/apply, or actual Keycloak permission matrix
  was verified. `docker`, `psql` and `pulumi` were not found in the Windows PATH.
- Existing dependency advisories were reported for AutoMapper, Aspire/MessagePack,
  plus transitive OpenTelemetry packages in the Pulumi project. No upgrade was
  performed as part of this work.

## Configuration and local artifacts

See `docs/lab9-identity-integration-checklist.md` and
`docs/lab9-provider-switching-checklist.md` for AppHost parameter names. Secrets,
initial passwords, key directories and mailbox contents must stay external.
Auth cookie flows require HTTPS. Windows Data Protection uses user-bound DPAPI;
non-Windows explicit file storage needs an appropriate deployment protection plan.

The checklists in `docs/lab9-*.md` contain detailed pending runtime cases. Some
older chunk files describe the state when written (for example, routes still
unmapped); this handoff and current code supersede those historical statements.
The provisioning checklist predates the added verified-email JWT claims.

Ignored probes are under root `obj/`, including a JWT harness, Keycloak API probe
and commit-history scripts. They are local scratch, not production or submission
artifacts. Do not execute the history scripts again.

## Git history caution

The initial full renumbering was reversed. Then, at the user's clarified request,
only the 27 Lab 9 `feat(...)` messages were changed to numbered descriptions.
Older history through `d09e4ff` is intact. The `fix(...)` message was left intact;
number gaps are intentional. Current committed HEAD before this save is
`efb3b7a` with message `[124]`. Use `[125] description` onward for new commits.
The save operation groups remaining changes as `[125]` identity provisioning,
`[126]` Aggregator OAuth credentials and `[127]` handoff/checklists. After those
commits, continue numbering at `[128]`.
Renamed local Lab 9 commit hashes differ from earlier conversation hashes.
Nothing was pushed, and the remote retains its original history. Do not force-push
or perform further rewrites without explicit authorization.

All delegated agents had completed their current chunks at the time of this save.
Review the three prepared change groups above, preserve them, and continue from
owner enforcement rather than regenerating existing code.
