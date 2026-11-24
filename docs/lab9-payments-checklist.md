# Lab 9 Payments demonstration checklist

The three endpoints are nonfinancial stubs. Integration into AppHost, solution,
Gateway and the shared cross-service authorization layer remains separate work.
The API currently supports Keycloak bearer tokens only, with the
`Authentication__Keycloak__Authority` realm URL configured externally. A plain
HTTP authority is accepted only in Development; use HTTPS outside Development.

- [ ] Build Payments.Api and configure a trusted Keycloak realm, issuer, JWKS,
      payments-api audience and real permission-bearing tokens in an isolated setup.
- [ ] Verify GET /payments needs payments:read, POST /payments/process needs
      payments:process and POST /payments/refund needs payments:refund under
      resource rsname payments-api. All three return 200 for matching grants.
- [ ] Missing, malformed, unsigned, expired, wrong-issuer and wrong-audience
      tokens return 401. Role names alone do not grant access; valid tokens without
      a required permission return 403 before entering the action.
- [ ] Use real issued admin/manager/user/guest tokens: admin can call all three;
      manager, user and guest receive 403 for all three. Record actual responses.
- [ ] Test absent/malformed/null/array/string authorization, missing/null/nonarray
      permissions/scopes, wrong types, oversized claim, wrong resource, wrong case,
      substrings and permissions split across different resources. These must yield
      403 without an exception. Exact matching on resource and scope is required.
- [ ] Test multiple authorization claims and duplicate/irrelevant permissions;
      one correctly formed grant may authorize the matching endpoint only.
- [ ] Verify issuer discovery/JWKS validation occurs locally after key caching and
      no Keycloak call is made for each authorization decision. Stop Keycloak once
      metadata/keys are cached and check a still-valid token, noting cache limits.
- [ ] Verify health probes remain available for intended infrastructure and no
      payment operation persists or sends funds. Do not record unredacted JWTs.
- [ ] Exercise identity-provider switching and all other business APIs only after
      the shared P3/P8 integration exists; this project currently does not provide
      Identity JWT or Duende modes or Auth ownership policies.
