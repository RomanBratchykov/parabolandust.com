# Lab 9 chunk 11: resend confirmation

Status: verification pending. The route mapping is not called by Program until
Identity storage, role initialization, mail and Gateway are configured.

- [ ] In an isolated relational Identity harness, register an unconfirmed user,
      map registration, confirmation and resend, and use valid antiforgery tokens.
- [ ] POST /auth/resend-verification with the user's email creates a confirmation
      message with the same Identity ID and a usable token; email is trimmed.
- [ ] Unknown email and already-confirmed user receive the same 200 response as
      an unconfirmed user; neither produces a message or changes account state.
- [ ] Confirm with a resent token, then resend and replay older tokens. The account
      stays confirmed and old tokens do not produce another confirmation.
- [ ] Missing/blank/oversized email, malformed JSON and extra fields fail with 400.
      Missing/mismatched CSRF cookie/header fails before mail or user mutations.
- [ ] Simulate mail failure and cancellation: no success is reported; account
      remains unconfirmed and a later resend can retry. Failures may leave a
      partial development mailbox file; inspect logs for secret exposure.
- [ ] Compare responses and timing under normal operation. Mail errors can expose
      whether a matching unconfirmed account exists; production integration must
      address durable delivery, enumeration resistance and rate limiting before
      exposing the route publicly. None of those is implemented in this chunk.
- [ ] Run the handler concurrently with confirmation and inspect mail/state. A
      message sent during the race may contain an already-invalidated token; the
      user must remain confirmed and the handler must not reset confirmation.
