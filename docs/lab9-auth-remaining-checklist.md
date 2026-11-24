# Lab 9 Auth session and user administration checks

Implementation status: Auth.Api built successfully with `dotnet build
Services/Auth/Auth.Api/Auth.Api.csproj --no-restore -m:1`. Runtime tests are
pending. The endpoint mappings must be registered by the integration owner;
IdentityDb migrations and role seeding must complete before these checks.

All tests below should use a disposable relational IdentityDb and a private
development mailbox, not existing user/business databases. Obtain a User and an
Admin access token plus refresh cookies. `IdentityJwt` is the sole permitted
scheme for these routes. Access JWTs have no server-side revocation and remain
valid until expiration even after logout, password/role change or deletion.

## Sessions and password

- [ ] Verify anonymous access to revoke, logout and change-password gives 401;
      missing/invalid CSRF cookie/header fails before mutation.
- [ ] Revoke caller's refresh cookie, confirm it is revoked and cleared, and
      reject it on refresh. An already revoked, unknown, or another user's cookie
      produces 403 and does not revoke another account's row.
- [ ] Logout with caller-owned cookie revokes and clears it. Missing cookie
      returns 204 with a deletion cookie; a different user's cookie returns 403.
- [ ] Wrong current password or invalid new password returns 400 with no password
      or session changes. Correct credentials change the password, revoke all
      existing refresh rows transactionally and clear the browser cookie.
- [ ] Race password change with login/refresh and verify the stamp and locking
      guarantees described in lab9-chunk14-checklist.md. Confirm a failed refresh
      revocation rolls back the password update using the same scoped context.
- [ ] Confirm a token lacking a `sub` claim cannot perform authenticated actions.

## User and role management

- [ ] Anonymous requests return 401; non-admin list, delete and role mutations
      return 403. Only the `IdentityJwt` scheme may satisfy these requirements.
- [ ] A User can get/update their own user by ID and fetch own session history,
      but receives 403 for another ID, including after caches are warm. Admin can
      access either. `/users/{id}/roles` is readable by any authenticated user as
      required by the lab. Missing existing users return 404 after authorization.
- [ ] User list defaults to first 25 records; page sizes above 100 and invalid page
      numbers fail. Password hashes, security stamps and tokens are absent.
- [ ] Update accepts only UserName and rejects extra role, email-confirmation,
      password, hash, security-stamp or other fields. Identity rejects duplicate
      or invalid names and old JWT claims retain their original values until expiry.
- [ ] Refresh history returns only ID and creation/expiry/revocation/replacement
      metadata; never token hashes, security stamps, raw values or unrelated rows.
- [ ] Admin may add/remove exactly `User` and `Admin`; lowercase or unknown roles
      fail, normal users cannot promote themselves, and roles in newly issued JWTs
      reflect changes. Existing JWT roles remain valid until token expiry.
- [ ] Delete a disposable user with a chain of rotated refresh tokens. The handler
      first clears replacement links in a transaction, then deletes via Identity;
      verify user/session/role rows are removed or all changes roll back on failure.
      Race deletion against refresh issuance and record actual provider behavior.
- [ ] Identity deletion and role changes do not invalidate already issued access
      JWTs; verify the platform's eventual access-revocation policy separately.
- [ ] Review telemetry for password, token or request-body leakage. Role/user
      mutations require bearer headers; no permissive credentialed CORS should be
      introduced. Production rate limiting and administrator bootstrap are pending.
