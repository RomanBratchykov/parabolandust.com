# Lab 9 interim backend authorization

The three APIs and Aggregator require an `Admin` role on all business HTTP and
business gRPC endpoints. HTTP and gRPC health endpoints remain anonymous. This
is a temporary fail-closed restriction: User, manager and other non-Admin flows
are intentionally unavailable until ownership links and per-operation policies
exist. The Aggregator does not yet attach bearer credentials to downstream gRPC
calls, so even an Admin dashboard request cannot be expected to work end to end.

- [ ] Configure Authentication__Provider and matching Jwt__Issuer, Jwt__Audience
      and Jwt__SecretKey in each standalone service and AppHost; other provider
      settings are described in lab9-gateway-auth-checklist.md.
- [ ] Against each backend directly, verify missing/invalid token -> 401 (gRPC
      Unauthenticated), valid User token -> 403 (PermissionDenied), valid Admin
      token reaches business handling. Cover every controller, stats endpoint and
      three business gRPC methods, including request bodies carrying user IDs.
- [ ] Verify `/health/live`, `/health/ready`, and the standard gRPC health protocol
      remain accessible without tokens, including Aggregator readiness probes.
- [ ] Verify direct endpoints reject spoofed X-User-Id. Admin retains broad access
      under the temporary policy; this does not implement owner isolation.
- [ ] Restore scoped gRPC credentials and the dashboard flow with least privilege,
      then prove token propagation without cross-request leakage. Authorize the
      inbound caller and ownership before looking up or returning a cached dashboard.
- [ ] Implement a trusted `(issuer, subject) -> Accounts user ID` link, service
      identity policy, per-operation permission enforcement and network isolation.
      Replace the Admin-only policy and test real User/manager/guest use cases;
      do not equate authentication alone to authorization.
- [ ] Exercise production configurations for IdentityJwt, IdentityServer and
      Keycloak separately. External role mapping is not implemented; `Admin` is
      case-sensitive, and Keycloak's `admin` role must not be silently treated as
      an Identity `Admin` role.
- [ ] Existing integration suites that start these services must supply valid
      authentication configuration and valid role-bearing test tokens. Update tests
      only for the intended contract; retain anonymous health tests.
