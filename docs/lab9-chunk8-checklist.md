# Lab 9 chunk 8: registration handler

Status: code only, unverified. No builds, tests, application launches, registration
requests or database commands were run. All checks are pending for another model.

Adds RegisterRequest and MapAuthRegistration. The mapping is deliberately not
called from Program until IdentityDb/provider setup, migrations, role seeding and
mail registration are wired. The current running app therefore does not yet
expose POST /auth/register. Provider choice and Accounts identity linking remain
unresolved; no complete registration-to-domain provisioning is claimed.

## Harness preparation

- [ ] Restore/build Auth.Api.
- [ ] Use a disposable relational Identity database with the chosen provider,
      migrations, AddAuthIdentity, JWT/antiforgery/Data Protection configuration,
      seeded User/Admin roles, and a development email pickup directory.
- [ ] Map registration in the harness with app.MapAuthRegistration(). Ensure the
      scoped ApplicationDbContext injected into the handler is the same context
      used by UserManager's EF store so both writes share its transaction.
- [ ] Obtain the antiforgery cookie/token via /auth/csrf-token and send the token
      in X-CSRF-TOKEN. Anonymous registration requires no access JWT.

## Registration behavior

- [ ] Valid username/email/password creates exactly one Identity user, a password
      hash and User membership. EmailConfirmed=false; LockoutEnabled=true.
- [ ] Response is 201 with a /users/{IdentityId} Location and only Id, UserName,
      Email. No password/hash, confirmation, access or refresh token is returned;
      no session cookie is issued by this handler.
- [ ] Confirmation mailbox file contains the same Identity ID/email and a token
      generated through UserManager. Verify token validity in the Identity harness.
- [ ] Whitespace-only or missing username/email/password and oversized
      username/email fail with 400. Leading/trailing username/email whitespace
      is trimmed; passwords are passed unchanged to Identity.
- [ ] Invalid email/username and password-policy failures return validation
      problems without creating a user. Missing/malformed JSON is rejected.
- [ ] Duplicate username/email registrations return a deliberate validation error
      in the sequential case. Race duplicate requests and report the actual
      provider behavior; provider-specific uniqueness exception mapping is still
      pending and must not be claimed implemented here.
- [ ] Extra fields such as roles, EmailConfirmed, PasswordHash or Id are rejected
      by JSON deserialization; a caller cannot self-assign Admin or confirmed state.
- [ ] Missing/wrong antiforgery cookie/header fails before user creation or mail.

## Transaction and recovery

- [ ] Force role-assignment failure: the new user and membership are rolled back.
      Force confirmation-token-generation failure: the same rollback occurs.
- [ ] Cancel before commit and verify no partial user persists. If cancellation
      occurs after commit, document the committed account and undelivered mail.
- [ ] Make the pickup directory unwritable: registration must not report 201;
      the committed unconfirmed account remains. The resend endpoint is future
      work, not an existing recovery route. No durable mail outbox is implemented.
- [ ] Inspect logs/evidence: no request password, hash or confirmation token is
      logged. Do not destructure-log RegisterRequest or mailbox contents.
- [ ] Confirm no Accounts numeric ID is inferred from the Identity string ID and
      no credentials are copied to Accounts/events. Cross-service owner access
      must remain denied until an explicit issuer/subject link is provisioned.
