# Accounts identity-link resolver and provisioning

The internal self endpoints operate on the issuer/subject of a locally validated
IdentityJwt only; callers cannot supply an account ID. They must be routed only
over a trusted service boundary. The current Auth JWT does not carry `email` and
`email_verified`, so creation intentionally returns 403 until Auth emits trusted
verified-email claims. Do not make the endpoint reachable publicly until network,
rate limit, profile validation and lifecycle decisions are reviewed.

- [ ] Apply identity-link migration to a disposable Accounts DB. Configure the
      IdentityJwt issuer, audience, key, messaging and service dependencies.
- [ ] No/invalid JWT -> 401. Unknown, revoked, deleted link -> 403. A valid linked
      subject gets its own numeric ID only, regardless of input or spoofed headers.
- [ ] Provision with exactly one `iss`, `sub`, `unique_name`, `email`, and signed
      `email_verified=true`: a new identity-managed account and link appear in one
      transaction, no local password or OAuth fake credentials are stored.
- [ ] Existing Auth JWT without verified email -> 403 with no database write.
      Wrong/unknown issuer, duplicate claims, missing/unverified email, unsupported
      provider, oversized claims and invalid email must not create an account.
- [ ] Existing account email/name conflict returns 409 and never associates the
      identity with that account. Same issuer/subject concurrently provisioned
      returns one Account ID and one link; different issuers remain distinct.
- [ ] Revoked pair and soft-deleted user never resurrect, even on repeated provision.
      Force failure during account/link creation and verify both roll back.
- [ ] A successful or retried provision publishes an AccountCreated snapshot through
      the existing post-commit broker path. Simulate failed publication: caller
      sees failure, source survives, retry republishes. No transactional outbox
      exists, and a crash without retry can still lose projection delivery.
- [ ] Check source-version ordering and repeated events in Newsletters/GameStats,
      cache invalidation and race with concurrent profile edits/deletion.
- [ ] Resolver must be invoked before personal data or cache access in each API;
      this chunk does not yet integrate those owner checks, change Admin-only
      policies, or authenticate Aggregator service credentials.
