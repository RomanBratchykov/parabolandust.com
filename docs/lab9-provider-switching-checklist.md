# Lab 9 AppHost provider selection checklist

The AppHost propagates one selected provider to Gateway, Accounts, Newsletters,
GameStats and Aggregator. `Authentication:Provider` (environment
`Authentication__Provider`) defaults to `IdentityJwt` and accepts `IdentityJwt`,
`IdentityServer`, or `Keycloak` exactly. Invalid values stop AppHost startup.
Auth management remains on its own Identity JWT and Payments remains Keycloak-only.

For IdentityJwt the business services receive the existing Auth issuer/audience
and the same secret parameter, `auth-jwt-secret`. For either external provider,
set Aspire parameters `authentication-authority` to the browser-visible issuer
and `authentication-audience` to an audience actually present in tokens intended
for the Gateway and existing business APIs. Set the separate
`keycloak-realm-authority` parameter for Payments, including in IdentityJwt mode.
The shared external audience is a configuration boundary, not a per-API audience
design. Payments independently requires `payments-api` as its audience.

The externally resolved authority must be reachable by each process and produce
the exact token issuer; container-internal and browser addresses must be reconciled
by the deployment. Set `Authentication__RequireHttpsMetadata=false` only in a
Development environment where HTTP is intentional; each service rejects HTTP
otherwise. Secrets are passed as Aspire secret parameters, not source values.

- [ ] Build AppHost; start an isolated stack with required secret parameters and
      persistent volumes. Confirm all business services and Gateway receive the
      same provider; invalid provider/invalid boolean fail startup.
- [ ] In IdentityJwt mode verify valid Auth token works at the Gateway, invalid
      issuer/audience/signature yields 401, and external issuer tokens are rejected.
      Gateway and existing APIs still require interim Admin-only policies; test
      expected denials explicitly. Auth endpoints keep their own JWT scheme.
- [ ] In IdentityServer and Keycloak modes supply the correct authority/audience,
      test discovery and JWKS with HTTPS, valid token validation and rejection of
      wrong/inactive issuers and audiences. Attempt HTTP metadata outside
      Development and verify services reject startup.
- [ ] Current backend `InterimAdmin` policies require case-sensitive `Admin` and
      do not implement Keycloak lowercase roles, OAuth scopes or ownership. Expect
      403, not working external-provider business authorization, until P3/P5/P8
      replaces them. The Aggregator incoming-bearer handler rejects non-IdentityJwt
      providers, so external provider end-to-end composition is not implemented.
- [ ] Payments independently validates Keycloak `payments-api` audience and
      resource permissions. The Gateway's single active issuer/audience policy can
      prevent routing Payments in other modes; do not claim all provider/Payments
      combinations work. Test audience alignment with real tokens before enabling.
- [ ] In each mode verify service and Gateway startup, secret propagation, health,
      backend direct-token validation and error statuses. Do not save credentials
      or raw JWTs in logs/evidence. No live provider or container tests were run
      by the implementation worker.
