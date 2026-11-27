# Lab 9 Duende IdentityServer verification

Status: project builds; PostgreSQL migrations were generated for all three
contexts but have not been applied to a real database. Tests below remain pending.
Use a disposable database named IdentityServerDb, separate from Auth/business data.

- [ ] Wire the project to AppHost and solution; supply `ConnectionStrings:IdentityServerDb`,
      absolute `DataProtection:KeyPath`, `Clients:Postman:Secret`,
      `Clients:Aggregator:Secret`, and `Clients:Swagger:RedirectUri` via secret
      configuration. Use HTTPS and keep secrets outside Git.
- [ ] Optionally provision a local demo user via `DemoUser:UserName`, `DemoUser:Email`,
      `DemoUser:Password` in Development. Link its actual issuer/subject explicitly
      to an Accounts ID before any owner-based API access; no linking is implemented.
- [ ] Run the three migrations against the disposable store. Verify standard
      Identity, Duende configuration and persisted-grant tables plus three distinct
      EF migration-history tables. Restart without duplicate seed data.
- [ ] Verify discovery/JWKS, browser login/consent/logout and configured client
      registrations. Postman uses authorization code, Swagger code + mandatory PKCE,
      Aggregator client credentials only; inspect scopes and real API audiences.
- [ ] Confirm Aggregator receives only read scopes and no human user identity.
      Actual Auth/Gateway/backend provider configuration, service credentials,
      API authorization and domain mapping are separate integration work.
- [ ] Seed inserts missing entities only. Existing client secrets, redirects, scopes
      and resource definitions are deliberately not updated on restart; verify a
      controlled rotation/update process before changing production configuration.
- [ ] Test signing keys, encrypted key persistence and shared Data Protection over
      restart. Filesystem key persistence on non-Windows is not encrypted by this
      code. Configure adequate secure storage and replica access before deployment.
- [ ] Review Quickstart UI surfaces and disable demo/diagnostic features not needed
      for the deployment; confirm redirects, consent, cookie settings and CSRF.
- [ ] License: see https://docs.duendesoftware.com/general/licensing/. Configure a
      valid license where required; this project provides no production entitlement.
