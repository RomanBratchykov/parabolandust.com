# Lab 9 chunk 14: coordinate sessions with account security changes

Status: runtime verification pending. Refresh sessions now carry the Identity
security stamp captured at issuance. Issuance and rotation update the matching
user's security stamp to itself within their transaction to acquire a user-row
write lock before inserting a session. This relies on actual provider locking
and affected-row behavior; it must be verified on the chosen production provider.
No migration or backfill is included: no Auth database has been wired yet.

- [ ] Build Auth and inspect migration generated later: SecurityStamp is required
      on RefreshTokens. If an Auth database already exists, explicitly invalidate
      legacy sessions or develop a safe migration; do not trust an empty stamp.
- [ ] Confirm a valid login issues a session matching the user's stamp. A stale
      stamp, unconfirmed email, active lockout or enabled two-factor flag makes
      issuance/rotation fail and issues no usable successor.
- [ ] In real relational transactions coordinate barriers so login validates the
      old password then pauses, reset commits, then login tries to issue. Login
      must return 401 and persist no new session.
- [ ] Invert ordering: issue commits first, then reset commits. Reset must revoke
      the newly issued session. Repeat for rotation before and after reset.
- [ ] Race reset and rotation repeatedly on the chosen provider. Verify no live
      session bearing the old stamp survives reset and no deadlock results in a
      success response. Record provider-specific deadlock/serialization failures.
- [ ] Race two rotations of one session and verify only one successor commits;
      failure rolls back any inserted successor. A token that expires while waiting
      for the user lock must be rejected after the lock is acquired.
- [ ] Check issuing for one user doesn't block unrelated users and a failed reset
      rolls back both password and revocations. Verify stamp changes from other
      Identity operations invalidate existing sessions as intended.
- [ ] Confirm write-lock behavior for no-op UPDATE, row-count semantics, lockout
      comparisons and transaction isolation on each supported provider. Do not use
      the EF in-memory provider as proof. Check concurrency with real connections.
- [ ] Issued access JWTs remain usable until expiry. Authentication and session
      endpoints remain unwired until provider, migrations and integration checks.
