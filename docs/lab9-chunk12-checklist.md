# Lab 9 chunk 12: forgot-password handler

Status: runtime checks pending. The route is not mapped by Program until the
Identity store and mail sender are integrated. This chunk does not reset a
password, revoke refresh sessions or implement a production mail transport.

- [ ] Restore/build Auth.Api and configure a disposable Identity database and
      development mailbox. Map forgot-password with existing antiforgery setup.
- [ ] Confirmed account: POST email with valid antiforgery cookie/header returns
      200 and creates a reset-password message with the correct Identity ID,
      recipient and an Identity-generated token. Surrounding email spaces are
      trimmed. A successful request does not change a password or revoke sessions.
- [ ] Unknown and unconfirmed accounts return the same status and response body
      as confirmed accounts; neither generates a mailbox message.
- [ ] Missing, whitespace, over-256-character email, malformed JSON and extra
      fields return 400. Missing/mismatched antiforgery credentials fail before
      sending mail. Passwords and tokens never appear in ordinary telemetry.
- [ ] Simulate a mailbox failure and cancellation; failure is not reported as a
      successful delivery. A partial development file may remain. Sender errors
      and response timing can disclose account existence: production integration
      must address enumeration resistance, rate limiting and durable delivery.
- [ ] Once reset is implemented, verify the emailed token works once, expires as
      configured, changes the password and revokes all existing refresh sessions.
      This handler alone provides none of those reset guarantees.
