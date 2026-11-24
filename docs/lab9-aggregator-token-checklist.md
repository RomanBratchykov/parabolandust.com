# Lab 9 Aggregator per-call bearer propagation

The three business gRPC clients obtain the authenticated IdentityJwt Admin token
from the active HTTP request and add it through gRPC per-call credentials.
Health clients do not use the callback. There are currently no business HTTP
clients in the Aggregator; if they are added, implement equivalent per-request
credential propagation without mutable shared headers. IdentityServer/Keycloak
client-credentials acquisition remains unimplemented and fails closed here.

All runtime verification pending:

- [ ] Build the solution; configure matching IdentityJwt issuer, audience and
      secret on Aggregator and all three APIs with HTTPS discovery/endpoints.
- [ ] Anonymous/invalid/User inbound dashboard requests fail before cache access;
      valid Admin reaches cold business calls and all three backends validate the
      forwarded bearer independently. Confirm warm cache hits still require Admin.
- [ ] Run concurrent requests with distinct Admin JWTs and trace a fake or isolated
      gRPC server: each business call has only its own exact bearer value, including
      retries and optional parallel calls. No token appears in logs or test evidence.
- [ ] Trigger gRPC health probes without inbound HTTP context; they send no bearer
      metadata and still work. Business calls without an authenticated HTTP context,
      malformed authorization header, User role, or unsupported provider fail closed.
- [ ] Verify configured HTTP/2 TLS enforcement, deadlines, cancellations, circuit
      behavior and retries remain intact. Request credentials must never be sent to
      an untrusted service or unsecured HTTP endpoint.
- [ ] In non-IdentityJwt modes, calls fail rather than using a user's token as a
      service identity. Implement client-credentials acquisition, expiry-aware
      caching and scoped service permissions before enabling those modes.
- [ ] Replace temporary Admin-only access only after implementing a trusted
      (issuer, subject) to numeric account link and owner checks before cache use.
      Forwarding a valid credential does not itself enforce ownership or permissions.
