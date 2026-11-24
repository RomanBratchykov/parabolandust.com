# Lab 9 chunk 9: one-use email confirmation handler

Status: unverified code. No builds, tests, HTTP calls, app launches or database
commands were run. All checks below are pending for another model.

Adds VerifyEmailRequest and MapAuthEmailConfirmation for POST /auth/verify-email.
Like registration, this route mapping is not invoked by Program yet: provider
choice, IdentityDb setup, migrations and startup wiring remain pending.

Confirmation uses UserManager. EmailConfirmed and security-stamp rotation are
committed together, with Identity's existing concurrency stamp protecting stale
updates. Already-confirmed users are rejected before token validation. Rotating
the security stamp also invalidates other outstanding stamp-bound Identity
tokens, including password-reset tokens; it does not revoke issued access JWTs
or persisted refresh tokens.

## Harness setup

- [ ] Restore/build Auth.Api on .NET 10.
- [ ] Configure a disposable relational Identity store, the existing Identity
      policies, persistent Data Protection, antiforgery and development mailbox.
      Map registration and confirmation in the harness.
- [ ] Confirm the handler and UserManager store share the same scoped
      ApplicationDbContext, so both Identity writes use the transaction.
- [ ] Register an unconfirmed user and retrieve the confirmation token from the
      private pickup file. Obtain the antiforgery cookie/header pair over HTTPS.

## Validation and one-use behavior

- [ ] An anonymous POST with matching UserId/Token and CSRF cookie/header returns
      204, persists EmailConfirmed=true and changes the security stamp.
- [ ] Replaying the same token returns 400 with no further state change.
- [ ] Generate multiple confirmation tokens before confirmation; after one
      succeeds, all older ones fail. Future resend must not unset EmailConfirmed
      or make a consumed confirmation credential usable again.
- [ ] Invalid/expired tokens, wrong user ID, another user's token, already-confirmed
      and unknown users all fail with the same generic 400 problem title.
- [ ] Missing/whitespace fields, oversized IDs/tokens, extra JSON fields and
      malformed JSON are rejected without user mutation or token disclosure.
- [ ] Invalid/missing CSRF cookie/header returns 400 before Identity writes.
- [ ] No access or refresh token/cookie is issued. Confirmation is not login.

## Concurrency and rollback

- [ ] Race two confirmations for the same user with independent scoped stores:
      at most one succeeds; the stale writer is rejected by Identity concurrency
      handling. Record actual provider behavior, including serialization errors.
- [ ] Force security-stamp update failure after ConfirmEmailAsync succeeded:
      transaction rollback must retain EmailConfirmed=false and the previous
      security stamp in the database.
- [ ] Cancel before commit and confirm neither security mutation persists. If
      cancellation races a completed commit, record actual persisted state.
- [ ] An unexpired confirmation remains usable after a same-key-ring restart;
      after a successful confirmation, a restart does not permit replay.
- [ ] Previously generated password-reset tokens become invalid after confirmation
      stamp rotation. Existing signed access JWT behavior remains expiry-based.
- [ ] Confirm token/password contents are absent from ordinary telemetry and
      response bodies; save only redacted evidence.

Reference: [Microsoft UserManager confirmation API](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.usermanager-1.confirmemailasync).
