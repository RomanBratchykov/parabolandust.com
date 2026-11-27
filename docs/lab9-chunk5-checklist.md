# Lab 9 chunk 5: refresh cookies and CSRF foundation

Status: unverified implementation. No builds, tests, HTTP requests, or application
launches were run. A different model must execute and record these checks.

Implemented: refresh-cookie read/write/delete helpers, antiforgery configuration,
GET /auth/csrf-token, and an endpoint filter for future session routes. Login,
refresh, logout and revoke endpoints are still pending, so the filter is not yet
attached to a production session route. No protection of nonexistent routes is
claimed. Database/provider selection remains pending.

## Build and cookie checks

- [ ] Restore/build Auth.Api. Run on HTTPS with externally supplied JWT settings.
- [ ] In an isolated harness, write a RefreshTokenGrant using RefreshTokenCookie.
      Inspect Set-Cookie: __Secure-parabolandust-refresh, HttpOnly, Secure,
      SameSite=Strict, Path=/auth, no Domain, expiry matching the grant.
- [ ] Read returns the cookie value; missing cookie returns null.
- [ ] Clear emits an expired cookie with matching name/path/domain/security policy.
      A real browser subsequently stops sending the cookie to /auth routes.
- [ ] Cookies are inaccessible through document.cookie. Refresh cookie is not sent
      to /users or other routes outside /auth. Plain HTTP is not a supported
      browser session transport; do not disable Secure to make a check pass.
- [ ] System.Text.Json serialization of RefreshTokenGrant excludes Token. Never
      destructure-log it: JsonIgnore does not prevent other logging serializers.

## CSRF protocol

- [ ] GET /auth/csrf-token anonymously returns requestToken and sets the separate
      __Host-parabolandust-csrf cookie: Secure, HttpOnly, Strict, Path=/, no Domain.
      Confirm the response is not cacheable and contains no refresh/access token.
- [ ] Retain the antiforgery cookie and put requestToken in X-CSRF-TOKEN for POST
      session calls. For Postman, enable its cookie jar and use the same HTTPS
      origin. Fetch a fresh token after authentication identity changes.
- [ ] Attach SessionAntiforgeryFilter to an isolated POST harness endpoint with
      AddEndpointFilter<SessionAntiforgeryFilter>(). Matching cookie/header must
      reach its handler; absent header, absent cookie, mismatched pair and
      malformed values return 400 before any session mutation.
- [ ] Request tokens obtained for another authenticated principal must fail.
- [ ] Exercise an untrusted cross-origin browser form/fetch and verify it cannot
      perform the protected action. Do not add permissive credentialed CORS.
- [ ] Future login/refresh/revoke/logout integration must attach the filter before
      any credential/session mutation. For MVC controllers use the built-in
      ValidateAntiForgeryToken attribute instead of this minimal-API filter.
- [ ] Persistent Data Protection keys, Gateway auth routing and HTTPS remain
      integration work. Verify antiforgery survives intended restarts only after
      key persistence is implemented; do not infer persistence from this chunk.

Reference: [Microsoft antiforgery guidance](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
