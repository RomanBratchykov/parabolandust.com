-- Apply to an existing accounts_db_p1 after taking a backup. The initializer
-- skips existing databases; this upgrade is intentionally not run automatically.
BEGIN;

ALTER TABLE accounts.users
    ADD COLUMN IF NOT EXISTS identity_managed BOOLEAN NOT NULL DEFAULT FALSE;

-- Legacy rows keep their original password/OAuth requirement. New managed rows
-- have neither: the external identity provider owns their credentials.
ALTER TABLE accounts.users DROP CONSTRAINT IF EXISTS ck_users_auth;
ALTER TABLE accounts.users ADD CONSTRAINT ck_users_auth CHECK (
    (identity_managed AND password_hash IS NULL AND oauth_provider IS NULL)
    OR (NOT identity_managed AND (password_hash IS NOT NULL OR oauth_provider IS NOT NULL)));

-- Keep revoked pairs as tombstones so they cannot silently bind to another user.
CREATE TABLE IF NOT EXISTS accounts.external_identities
(
    issuer TEXT COLLATE "C" NOT NULL,
    subject TEXT COLLATE "C" NOT NULL,
    user_id BIGINT NOT NULL REFERENCES accounts.users(user_id),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    revoked_at TIMESTAMPTZ,
    CONSTRAINT pk_external_identities PRIMARY KEY (issuer, subject),
    CONSTRAINT ck_external_identity_issuer CHECK (octet_length(issuer) BETWEEN 1 AND 1024),
    CONSTRAINT ck_external_identity_subject CHECK (octet_length(subject) BETWEEN 1 AND 512),
    CONSTRAINT ck_external_identity_revoked_at CHECK (revoked_at IS NULL OR revoked_at >= created_at)
);

CREATE INDEX IF NOT EXISTS ix_external_identities_user
    ON accounts.external_identities (user_id);

COMMIT;
