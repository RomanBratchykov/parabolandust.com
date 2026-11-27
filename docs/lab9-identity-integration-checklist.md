# Lab 9 Identity integration checklist

Status: Auth and AppHost compile. No database migration, Aspire orchestration or
HTTP login flow has been verified against live infrastructure in this chunk.

The Identity store is a separate PostgreSQL `identity_db` in the platform
PostgreSQL resource. This follows the project database rule and adapts the Lab 9
source's SQL Server wording; grading acceptance of that adaptation is unverified.
Business databases are not used as Identity stores.

Configure Aspire parameters using the AppHost user-secrets store or equivalent:
`postgres-password`, `mongo-password`, `auth-jwt-secret` (at least 32 UTF-8
bytes), `auth-data-protection-key-path`, `auth-email-pickup-path`,
`keycloak-admin-user`, `keycloak-admin-password`,
`identityserver-data-protection-key-path`, `identityserver-postman-secret`,
`identityserver-aggregator-secret`, and `identityserver-swagger-redirect-uri`.
All configured filesystem paths
must be absolute, private, persistent locations outside the repository. The mail
sender is Development-only. Non-development deployments need a real sender.
Do not put secrets or security tokens in screenshots or source control.

An optional development-only administrator can be configured in the Auth
project's user-secrets as `BootstrapAdmin:UserName`, `BootstrapAdmin:Email` and
`BootstrapAdmin:Password`. Supplying any one requires all three. Initial startup
creates a confirmed Admin; repeated startup accepts only the same existing Admin
and email, and never promotes a pre-existing non-admin account. Configure through
secret tooling without writing passwords into source or shell history.

Gateway retains HTTP port 5000 and adds HTTPS port 5001. Browser Auth requests
must use HTTPS so Secure cookies work. Trust the local development certificate if
needed. The backend APIs are not yet fully protected by authentication and owner
policies: do not expose this partial implementation as a secured deployment.

- [ ] Start Aspire against isolated databases and verify `IdentityDb` is distinct.
      Inspect actual migration history and Identity/refresh tables; don't assume
      that generating a migration applied it.
- [ ] Confirm repeated startup retains users, sessions, keys and one User/Admin role.
      Inspect both Windows and Linux key protection/storage behavior.
- [ ] Exercise CSRF, register, confirmation, login and refresh through the HTTPS
      Gateway; run chunk 8–15 checklists against a real relational database.
- [ ] Verify all root and nested `/auth` and `/users` routes forward correctly;
      `/api/users` must continue to target Accounts.
- [ ] Inspect the AppHost's HTTPS endpoint/service discovery and Gateway reverse
      proxy certificate behavior. Confirm secure cookies are set, sent and cleared.
- [ ] Confirm readiness reflects Auth database and migration failures; current
      readiness does not register an explicit IdentityDb health probe after startup.
- [ ] Provision Keycloak via Pulumi and validate its actual token permissions.
      Payments requires a Keycloak permission token; the default Gateway uses
      IdentityJwt, so a provider/audience configuration is required before a
      Payments request can succeed end to end.
- [ ] Verify the separate IdentityServerDb migrations, discovery and browser
      flows with the [Duende checklist](lab9-duende-checklist.md). Its OAuth
      clients are seeded only when absent and need a separate rotation procedure.
- [ ] Apply no production/shared database resets and retain evidence with secrets
      redacted. Validate the remaining authorization and business-service routes.
