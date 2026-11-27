START TRANSACTION;
ALTER TABLE newsletter.users_copy ADD source_version bigint NOT NULL DEFAULT 0;

CREATE TABLE newsletter.processed_events (
    event_id uuid NOT NULL,
    processed_at timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT "PK_processed_events" PRIMARY KEY (event_id)
);

ALTER TABLE newsletter.users_copy ADD CONSTRAINT ck_users_copy_source_version CHECK (source_version >= 0);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261119110140_AddAccountEventInbox', '10.0.4');

COMMIT;
