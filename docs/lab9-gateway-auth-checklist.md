# Lab 9 Gateway authentication baseline

Build: `dotnet build Platform.ApiGateway/Platform.ApiGateway.csproj -m:1`
succeeded without warnings or errors. A temporary local Gateway using IdentityJwt
returned 401 for unauthenticated /api/users/1, /api/donations/1,
/api/newsletters, /api/aggregator/1 and a malformed bearer token; /health/live
returned 200. No downstream services were started for that check.

All further checks are pending:

- [ ] Configure `Authentication:Provider` as IdentityJwt, IdentityServer or
      Keycloak. For IdentityJwt, configure matching `Jwt:Issuer`, `Jwt:Audience`
      and `Jwt:SecretKey` on Auth and Gateway. For external providers configure
      `Authentication:Authority`, `Authentication:Audience`, and HTTPS metadata.
      HTTP discovery is permitted only in Development when explicitly configured.
- [ ] Verify every business route rejects absent, malformed, expired, unsigned,
      wrong-key, wrong-audience and wrong-issuer tokens with 401. A valid token
      reaches a real backend with its Authorization header intact.
- [ ] `/auth/**` and `/users/**` must reach Auth, including root paths, while
      `/api/users/**` continues to reach Accounts. Auth must perform its own
      authentication and authorization on protected endpoints.
- [ ] Send X-User-Id to all routes and inspect the proxied request; the header
      must be removed. Forwarded bearer tokens and Set-Cookie must be preserved.
- [ ] Switch provider mode, then verify valid tokens from an inactive issuer are
      rejected. Test IdentityServer and Keycloak discovery, signature rotation,
      audience and HTTPS policy using real issued tokens.
- [ ] Gateway authentication is only a baseline: independent backend JWT checks,
      owner linking, role/scope/permission enforcement, gRPC credentials and
      actual network isolation remain required. It does not make direct backend
      access safe and does not authorize one user's request for another's data.
- [ ] Gateway-to-backend TLS/HTTP routing and Data Protection antiforgery/cookies
      must be verified across real proxy boundaries; public Gateway currently
      uses HTTP in AppHost and secure cookies require browser-facing HTTPS.
