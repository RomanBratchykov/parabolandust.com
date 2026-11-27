# Lab 9 chunk 15: refresh endpoint

Status: runtime verification pending. MapAuthRefresh is not called in Program;
IdentityDb/provider setup, migrations and Gateway wiring are still pending.

- [ ] Build Auth. In an isolated relational setup, map login/refresh with Identity,
      token services, cookies, antiforgery and a confirmed account.
- [ ] Login then POST /auth/refresh-token with the refresh and antiforgery cookies
      and matching X-CSRF-TOKEN header. Verify a new access JWT and secure refresh
      cookie; the old token is revoked and never included in JSON.
- [ ] Missing, expired, malformed, revoked and reused refresh cookies return 401;
      missing or invalid antiforgery credentials return 400 before rotation.
- [ ] Concurrent refresh of one cookie creates at most one successful replacement;
      inspect relational rows and test the stamp-lock races in chunk 14.
- [ ] Missing/deleted, unconfirmed, locked or two-factor-enabled accounts receive
      401 without a delivered replacement; a replacement already committed by
      rotation is revoked when account state fails the post-rotation check.
- [ ] Confirm roles in the fresh access JWT come from current Identity roles and
      responses have no-store/no-cache headers; no token is logged.
- [ ] Simulate cancellation, token signing failure and response loss after rotation.
      Rotation commits before JWT creation and HTTP delivery: these failures may
      leave an inaccessible live replacement and consume the old cookie. Document
      behavior; no atomic HTTP-delivery guarantee is implemented.
- [ ] Race password reset with rotation and JWT issuance. Stamp locks protect the
      refresh row; a JWT already issued around a later reset can remain valid until
      expiry. Verify the intended access-token revocation policy before integration.
