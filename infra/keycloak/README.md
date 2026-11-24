# Lab 9 Keycloak infrastructure

This C# Pulumi program declares `DevRealmDrivers`, three Keycloak authorization
resource servers, the thirteen source permission scopes, role policies, clients,
and one temporary-password user per realm role. Business APIs must independently
validate issuer, audience and granted permissions; audiences alone are not grants.

## Configuration

Install .NET 10, the Pulumi CLI, and run a compatible Keycloak instance reachable
from the machine running Pulumi. Create a new Pulumi stack and configure:

```text
keycloak:url                          Keycloak base URL, for example http://localhost:8080
keycloak:clientId                     admin-cli for a development password grant
keycloak:username                     bootstrap administrator
keycloak:password                     bootstrap password (secret)
parabolandust-keycloak:keycloakUrl     externally visible Keycloak base URL
parabolandust-keycloak:swaggerRedirectUri  exact HTTPS Swagger OAuth callback URL
parabolandust-keycloak:aggregatorClientSecret  unique client secret (secret)
parabolandust-keycloak:adminPassword   initial demo password (secret)
parabolandust-keycloak:managerPassword initial demo password (secret)
parabolandust-keycloak:userPassword    initial demo password (secret)
parabolandust-keycloak:guestPassword   initial demo password (secret)
```

Set sensitive values using `pulumi config set --secret` with an interactive
prompt or a secret-management workflow; do not place values in shell history,
source, screenshots, or stack exports. Stack YAML and `.pulumi` state are ignored
by this directory's `.gitignore`. Protect the Pulumi backend and its encryption
passphrase; secret marking does not remove the need for access controls.

`pulumi preview` and then `pulumi up` provision resources. Users must finish the
temporary-password change in a browser before their authorization-code flow.
Run `pulumi preview` again to check convergence. `pulumi destroy` removes managed
resources and must only be used when teardown is authorized and evidence saved.

## Permission token limitation

The role policies and scope permissions define grants per resource server.
Authorization Code tokens and the added audience mappers alone do **not** create
the `authorization.permissions` claim. Keycloak UMA requests an RPT from a
particular resource server; obtaining and forwarding grants for multiple resource
servers remains integration work. No code here fabricates permission claims or
performs per-request authorization lookups. Verify real RPTs against the complete
matrix before enabling the Keycloak mode or claiming Lab 9 complete.

The Aggregator service account receives only Orders and Products service-read
client roles and no Payments role or audience. Actual RPT and role-mapper behavior
must be checked against the running Keycloak version. Resource server OAuth client
credentials are managed by Keycloak and retained in Pulumi state as provider data.
Do not expose state, generated credentials or initial passwords.
