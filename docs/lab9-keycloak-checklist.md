# Lab 9 Keycloak Pulumi verification

The project builds with Pulumi.Keycloak 6.14.2 and Pulumi 3.116.2. Runtime
preview/apply, live token contents and the full permission matrix remain pending.
NuGet reports moderate vulnerabilities in transitive OpenTelemetry.Api 1.9.0 and
OpenTelemetry.Exporter.OpenTelemetryProtocol 1.9.0; assess compatible upgrades.

- [ ] Start the intended Aspire Keycloak and use an isolated Pulumi stack. Provide
      provider/admin configuration and all secret settings as described in README.
- [ ] Preview all proposed changes and confirm no existing/shared realm or user is
      overwritten. Inspect secret handling and state protection; then apply only to
      the designated development instance.
- [ ] Verify realm settings, 3 resource servers, 13 scopes and 4 realm roles. Inspect
      each scope permission, backing resource and role policy. Keycloak's default
      allow-all authorization resources/policies must not survive KeepDefaults=false.
- [ ] Verify manager has orders read/update/approve, products read/update/publish;
      user has orders read/create and products read; guest has products read only;
      admin has all 13. All 3 payments scopes deny manager/user/guest.
- [ ] Verify every new account has confirmed email, its designated role and a
      temporary password. Complete password change in a browser and exercise Code
      + PKCE. Reject absent/wrong verifier and an unregistered redirect.
- [ ] Obtain a service-account token for aggregator-service. Inspect actual roles,
      scopes and audiences; only Orders/Products service-read roles are assigned,
      with no payments audience or permissions. Exercise the service-role policies.
- [ ] Verify scope/resource argument representations and role IDs against the
      target provider/Keycloak release. Run preview again after apply for drift.
- [ ] Obtain real Keycloak UMA permission tokens and inspect resource names/scopes
      in authorization.permissions. Verify the multi-resource issuance/integration
      design, currently not implemented. Plain access tokens plus audience mappers
      are not proof of the permission claim.
- [ ] Execute the 52 role/route matrix requests only after business APIs enforce
      local permission policies and use the documented identity mapping. Capture
      statuses and redacted evidence, never tokens or credentials.
- [ ] Inspect outputs RealmUrl/AdminConsoleUrl/ClientIds/ClientSecrets and confirm
      ClientSecrets is marked secret and all provider state is protected. Do not
      expose Pulumi stack secrets or key material. Only destroy with authorization.
