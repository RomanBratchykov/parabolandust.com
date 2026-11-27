# Lab 9 chunk 7: development confirmation/reset mail

Status: unverified code. No builds, tests, app launches, mail sends or filesystem
checks were performed. Another model must execute and record these checks.

DevelopmentEmailSender implements IAuthEmailSender for confirmation and password
reset tokens. In Development, Program registers it and now requires an absolute
Email:PickupDirectory (environment: Email__PickupDirectory), in addition to JWT
settings and DataProtection__KeyPath from earlier chunks.

Use a private directory outside the repository and web root, separate from Data
Protection keys. Files are the mock mailbox and intentionally contain usable
security tokens. They are not application logs or submission evidence. This
chunk supplies no HTTP mailbox-reading route and does not send SMTP mail.

## Configuration and environment

- [ ] Restore/build Auth.Api on .NET 10.
- [ ] With Development environment and all required settings, startup registers
      IAuthEmailSender without creating mailbox files before any mail is sent.
- [ ] Missing/empty/relative Email__PickupDirectory fails Development startup.
      A valid absolute path containing spaces works.
- [ ] Production and Staging do not register the mock sender, do not require its
      pickup setting and do not write mailbox files. Direct construction of the
      mock sender outside Development fails. A real sender remains required
      before enabling email-dependent endpoints in those environments.
- [ ] Set filesystem access so only the intended developer/service identity can
      read the mailbox. The implementation inherits OS permissions and does not
      provision ACLs itself. Verify there is no static web serving of this path.

## Mail contents and failure behavior (isolated harness)

- [ ] Send confirmation for a user with an ID/email and a generated Identity
      token: exactly one randomly named JSON file contains the correct UserId,
      To, Purpose=verify-email, Token and UTC CreatedAt.
- [ ] Send reset mail: a separate file uses Purpose=reset-password and contains
      its reset token. No password hash, password or refresh token is included.
- [ ] Confirm emails/user IDs containing path punctuation cannot control the
      filename or escape the configured directory; filenames contain only a GUID.
- [ ] Parallel sends create distinct files without overwriting earlier messages.
- [ ] Missing ID/email/token is rejected before writing. A pre-cancelled request
      creates no message. Cancellation during writing and access-denied/disk
      failures propagate; partial files can remain and must not count as delivered.
- [ ] Inspect ordinary logs/traces: no confirmation/reset token or message body
      was emitted by this sender. Do not publish raw mailbox files as evidence.
- [ ] Once endpoints exist, complete registration/confirmation and forgot/reset
      using the mailbox tokens, and check lifetime/reuse behavior at the Identity
      endpoints. Sender output alone does not prove token validation or one-use.

Pending: provider choice, store/startup wiring, migrations and all mail-consuming
account lifecycle endpoints. No registration or reset endpoint is added here.
