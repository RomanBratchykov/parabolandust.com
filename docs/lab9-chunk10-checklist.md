# Lab 9 chunk 10: password login handler

Status: unverified implementation. No builds, tests, login requests, app launches
or database commands were run. All checks below belong to another model.

Adds LoginRequest and MapAuthLogin for POST /auth/login. The mapping is not invoked
by Program until IdentityDb setup, migrations, service registration and roles are
wired. This chunk does not resolve the SQL Server/PostgreSQL choice or provide
Gateway routing. It is not a claim of a runnable integrated login flow.

## Harness preparation

- [ ] Restore/build Auth.Api on .NET 10.
- [ ] Configure a disposable relational Identity database and factory, Identity
      options, User/Admin roles, JWT signer, refresh service, persistent Data
      Protection and antiforgery. Map registration, confirmation and login.
- [ ] Register a user, obtain the development confirmation mail and verify email.
      Send the antiforgery cookie and X-CSRF-TOKEN header on login over HTTPS.

## Credentials and lockout

- [ ] Anonymous login with the confirmed user's username/password succeeds.
      Surrounding username whitespace is trimmed; password is passed unchanged.
- [ ] Unknown username, wrong password and unconfirmed email return 401 without
      access token, refresh cookie or persisted refresh session.
- [ ] Five wrong passwords for a confirmed, lockout-enabled user activate the
      configured five-minute lockout; correct credentials cannot bypass it.
      After expiry, valid login succeeds and Identity resets the failure count.
- [ ] Set TwoFactorEnabled in the harness. Even a correct password must not issue
      a JWT/refresh cookie; this endpoint does not implement second-factor login.
- [ ] Null/missing/whitespace fields, username over 256 characters, malformed JSON
      and extra JSON properties fail without a successful session.
- [ ] Missing/mismatched antiforgery cookie/header fails before password checking
      or refresh issuance; attempt a cross-origin login-CSRF request too.

## Token and session boundary

- [ ] Success returns 200 JSON with only accessToken, tokenType=Bearer and
      expiresIn=900. Validate actual JWT lifetime, issuer/audience/signature,
      subject, username and current stored roles using chunk 1 checks.
- [ ] Refresh value appears only in Set-Cookie with the chunk 5 security attributes;
      database stores its SHA-256 hash and exactly seven days of lifetime.
- [ ] JWT roles come from UserManager, never submitted request fields. Changing
      stored role membership affects newly issued access JWTs.
- [ ] No Identity application-authentication cookie is issued by this handler.
      CheckPasswordSignInAsync validates credentials without cookie sign-in.
- [ ] Responses use Cache-Control: no-store and Pragma: no-cache. No request
      password, access JWT, raw refresh value or token-response body enters logs.
- [ ] Force JWT signing failure: no refresh row is created. Force refresh storage
      failure: no successful token response/cookie is sent.
- [ ] Simulate response disconnect after refresh commit: document the possible
      persisted session with no delivered cookie. HTTP delivery and database
      persistence are not atomic; no delivery guarantee is claimed.
- [ ] Successful login replaces the browser cookie; prior refresh sessions remain
      valid until explicitly revoked or expired. Session listing/logout/reset
      enforcement remain later chunks.
- [ ] No Accounts numeric ID is inferred from the Identity subject. Domain owner
      checks still require the separately approved issuer/subject link.

Reference: [Microsoft password-check API](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.signinmanager-1.checkpasswordsigninasync).
