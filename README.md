<h1>Welcome to the</h1>


```text
    ____                   __          __            ____             __
   / __ \____ __________ _/ /_  ____  / /___ _____  / __ \__  _______/ /_
  / /_/ / __ `/ ___/ __ `/ __ \/ __ \/ / __ `/ __ \/ / / / / / / ___/ __/
 / ____/ /_/ / /  / /_/ / /_/ / /_/ / / /_/ / / / / /_/ / /_/ (__  ) /_
/_/    \__,_/_/   \__,_/_.___/\____/_/\__,_/_/ /_/_____/\__,_/____/\__/.com
```
Which is a website for our game

## Lab 01 database setup

The lab uses three independent database contexts. Project 1 and Project 2 use
PostgreSQL; Project 3 uses MongoDB. Create the databases before running their
scripts. No foreign key crosses a database boundary.

### Requirements

- PostgreSQL 13 or newer (`createdb`, `psql`); Project 2 uses `gen_random_uuid()`.
- MongoDB and `mongosh`.

### PostgreSQL projects

Run from the repository root with a PostgreSQL role allowed to create schemas
and tables:

```sh
createdb game_accounts_db
psql -v ON_ERROR_STOP=1 -d game_accounts_db -f db/p1/schema.sql
psql -v ON_ERROR_STOP=1 -d game_accounts_db -f db/p1/procedures.sql
psql -v ON_ERROR_STOP=1 -d game_accounts_db -f db/p1/seed.sql

createdb game_newsletter_db
psql -v ON_ERROR_STOP=1 -d game_newsletter_db -f db/p2/schema.sql
psql -v ON_ERROR_STOP=1 -d game_newsletter_db -f db/p2/seed.sql
```

The schema and seed files can be rerun. PostgreSQL procedures are installed
with Project 1. Example calls (the final argument is the OUT value):

```sql
CALL accounts.usp_user_create(
  'new_player', 'new@example.test', 'demo-hash', NULL, NULL,
  'game-link-new-001', NULL, NULL
);
CALL accounts.usp_user_get_by_id(1001, NULL);

BEGIN;
CALL accounts.usp_donation_change_status(1, 'Completed', 1, 'operator');
COMMIT;
```

`usp_donation_change_status` permits Pending → Completed/Failed and Completed
→ Refunded. Other transitions raise a controlled exception. If a call fails
inside an explicit transaction, issue `ROLLBACK`; the failed transition does
not persist. Supply the current `row_version` to reject stale concurrent edits.

### MongoDB project

From the repository root:

```sh
mongosh game_stats_db db/p3/collections.js
mongosh game_stats_db db/p3/seed.js
```

The scripts create three collections and indexes, then insert stable sample
documents only when their keys are absent. MongoDB IDs that match SQL IDs are
logical references only.

### Data ownership and duplication

| Data | Owning context | Copy/use in another context | Consistency policy |
| --- | --- | --- | --- |
| Account ID, name, email | Project 1 — Accounts | `users_copy` in Project 2 | Updated from Project 1 account-change events; newsletter service never edits source identity. |
| `user_whoami` | Project 1 — Accounts | `stats_user` and run snapshot in Project 3 | Updated when Project 1 rotates the game identity; run snapshot remains historical. |
| Achievement definitions | Project 1 — Accounts | Achievement names in statistics | Project 1 owns definitions; game events update unlock state in Project 1 and statistics from the same event. |
| Run/player summary | Project 3 — Statistics | Embedded `player_snapshot` in a run | Snapshot captured at run creation; not independently edited. |
| Subscription email | Project 2 — Newsletter | `user_email_snapshot` in subscription | Snapshot used for delivery/audit; refreshed when the Project 1 account-copy event is processed. |

See [the ER diagrams](docs/erd.md) and [database domain/design notes](db/DATABASE.md).
