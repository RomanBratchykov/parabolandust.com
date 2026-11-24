# Lab 9 Aggregator OAuth credentials checklist

Status: runtime verification pending. In IdentityServer mode the Aggregator
requests a client-credentials token and attaches it to business gRPC calls.
IdentityJwt retains incoming bearer forwarding; Keycloak fails closed. Configure
`ServiceCredentials:TokenEndpoint`, `ClientId`, `ClientSecret`, and `Scopes` in
secret configuration; use the IdentityServer `aggregator_service` client and
minimum required read scopes. The token endpoint requires HTTPS except HTTP
loopback in Development. Never put secrets or tokens in evidence.

- [ ] Obtain a valid IdentityServer user token for an incoming authorized admin.
      An absent/invalid token or non-admin must not cause token acquisition or
      a privileged downstream call. Owner mapping is still unresolved, and the
      Aggregator currently has an interim Admin-only policy.
- [ ] Verify a real client_credentials request sends only configured read scopes,
      produces no human subject and delivers the resulting bearer to all three
      business gRPC clients. Health calls remain anonymous.
- [ ] The current backend InterimAdmin policy may reject the service token; API
      scope/service policies and audiences need integration before this is a
      usable end-to-end flow. This change does not grant or bypass access.
- [ ] Twenty concurrent calls share one cached token; after 80% of its stated
      lifetime or one hour, whichever is shorter, another token is acquired.
      Caller cancellation while waiting or during the request must propagate.
- [ ] Invalid status, malformed JSON, non-Bearer type, no expiry, absent secret,
      oversized response and invalid/insecure endpoint fail closed, without
      exposing provider response content, client secret or token in logs.
- [ ] Scopes that do not end in `.read` are rejected before sending a token
      request. This requires configured IdentityServer scopes to use that naming
      convention; adopt an explicit allowlist if the final contract differs.
- [ ] IdentityJwt forwards the validated incoming bearer as before. Keycloak and
      unknown providers cannot reuse IdentityServer or IdentityJwt credentials.
- [ ] Verify gRPC deadlines, cancellation, tracing and retry limits, including
      Auth server outage/recovery and a partially failed dashboard. HTTP business
      clients do not currently exist; implement credential forwarding if added.
- [ ] Cache keys are process-local; a client-secret change or permission reduction
      doesn't revoke an existing token. Restart or issuer-side revocation may be
      needed; test the intended secret rotation procedure.
