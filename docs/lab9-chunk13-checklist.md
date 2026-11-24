# Lab 9 chunk 13: password reset handler

Status: runtime verification pending. The handler is not mapped by Program.
Identity store/provider, migrations, Gateway and persistent mail integration are
still required. The Auth project build is recorded separately in the response.

- [ ] In a disposable relational Identity store, map forgot/reset endpoints with
      Identity options, antiforgery, mailbox and a shared scoped DbContext for the
      handler and UserManager. Seed a confirmed user with multiple refresh sessions.
- [ ] Get a reset token from the private mailbox and POST UserId, Token and a valid
      NewPassword with antiforgery cookie/header. Expect 204, new password works,
      old password fails, security stamp changes and existing refresh rows are revoked.
- [ ] Replay, wrong user, malformed/expired token, unconfirmed user and invalid
      password return generic 400; original password and refresh sessions remain.
      Identity validation details are intentionally not returned by this handler.
- [ ] Missing/oversized fields, extra JSON fields and invalid CSRF fail safely.
- [ ] Force failure during refresh revocation or before commit; password/stamp and
      all refresh revocations must roll back together. Check the actual relational
      provider and ensure the UserManager store uses the injected DbContext.
- [ ] Race two reset requests with one token; at most one password change succeeds.
      Capture provider concurrency or serialization failures without claiming all
      failures necessarily map to 400.
- [ ] Race reset against login and refresh rotation. Existing session issuance and
      rotation use independent contexts without a shared user lock or security-stamp
      binding. They can create a valid session concurrently with or after the bulk
      revocation using credentials validated earlier. This must be fixed and retested
      before wiring this reset handler as a complete session-revocation guarantee.
- [ ] Existing access JWTs remain valid until expiry; no JWT revocation is implied.
      Never record passwords, raw reset tokens or refresh values in evidence.
