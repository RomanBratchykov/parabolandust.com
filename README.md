<h1>Welcome to the</h1>


```text
    ____                   __          __            ____             __
   / __ \____ __________ _/ /_  ____  / /___ _____  / __ \__  _______/ /_
  / /_/ / __ `/ ___/ __ `/ __ \/ __ \/ / __ `/ __ \/ / / / / / / ___/ __/
 / ____/ /_/ / /  / /_/ / /_/ / /_/ / / /_/ / / / / /_/ / /_/ (__  ) /_
/_/    \__,_/_/   \__,_/_.___/\____/_/\__,_/_/ /_/_____/\__,_/____/\__/.com
```
Which is a website for our game

## Lab 01 domain and database boundaries

This videogame community site lets players register, connect a game identity,
read development updates, subscribe to news, donate, and review personal or
leaderboard statistics. The database design splits account ownership,
publishing, and game telemetry so each service can evolve without cross-service
joins or foreign keys.

| Context / database | Owned entities | Aggregate and lifecycle |
| --- | --- | --- |
| Accounts and game identity — PostgreSQL `accounts_db_p1` | `users`, `game_linking`, `achievement`, `user_achievements`, `donations` | `users` owns profile and login identity; `user_achievements` records durable unlocks; donations retain payment history and validated status changes. |
| Newsletter publishing — PostgreSQL `accounts_db_p2` | `admins`, `newsletters`, `newsletter_delivery_settings`, `users_copy`, `user_comments`, `subscriptions` | A newsletter owns its delivery settings and comments. Subscriptions join the newsletter and local account read-copy; `users_copy` is updated from account events and does not own profiles. |
| Player statistics — MongoDB `game_stats_db` | `stats_user`, `runs`, `game_events` | `stats_user` embeds bounded lifetime-stat groups; `runs` embeds a run snapshot and references a user; `game_events` references user/run IDs with flexible event payloads. |

Each entity has exactly one owning context. IDs that cross a context boundary
are logical references only. See [the ERDs, MongoDB examples, and detailed
duplication policy](docs/erd.md).

### Data duplication policy

| Duplicated field | Stored in | Owner/source | Consistency policy |
| --- | --- | --- | --- |
| `users_copy.user_id`, `user_name`, `user_email`, `is_deleted`, `source_updated_at` | Project 2 SQL | Project 1 `users` | Updated eventually from account lifecycle/profile events; `source_updated_at` tracks the source `updated_at`; Project 2 does not edit the copy's source fields. |
| `stats_user.user_id`, `runs.user_id`, `game_events.user_id` | Project 3 MongoDB | Project 1 `users` | Stable logical IDs propagated with account/game events; no cross-database FK. |
| `stats_user.user_whoami` | Project 3 MongoDB | Project 1 `users.user_whoami` | Refreshed on identity-rotation events; Project 1 owns edits. |
| `runs.player_snapshot.user_name`, `user_whoami` | Project 3 MongoDB | Project 1 `users` | Historical snapshot captured at run start and intentionally not refreshed. |
| `subscriptions.user_email_snapshot` | Project 2 SQL | Project 1 `users.user_email` (copied through `users_copy`) | Captured when the player subscribes; remains a consent/delivery snapshot if the account email later changes. |
| `stats_user.progress.achievements_got` | Project 3 MongoDB | Project 1 `user_achievements` and `achievement` | Read projection updated by achievement-unlocked events; Project 1 owns unlock membership and definitions. |

## Lab 01 database setup

Run the initial setup once:
```bash
./db/run-sql.sh --all
```

The script creates and initializes each database only when that database does
not already exist. On later runs it skips existing databases without executing
their schema, procedure, seed, or index scripts, so existing data and indexes
are left untouched. To intentionally rebuild the Lab 01 databases, use the
separate [drop script](db/drop-db.sh), then run setup again.

For a new database, the optional selectors are:

- ```-p``` to apply schema and procedures
- ```-s``` to apply schema and seed data
- ```--schema``` (or ```-c```) to create the schema only; ```--scheme``` is accepted as an alias
- ```--all``` to apply schema, procedures, and seed data

Choose the required selectors on the first run for a database. Once a database
exists, the script intentionally skips it; it is an initializer, not a migration
runner.

To drop the three Lab 01 databases while keeping the containers and volumes,
run:

```bash
./db/drop-db.sh
```

This removes `accounts_db_p1`, `accounts_db_p2`, the unused PostgreSQL
`mydb` created by the Compose configuration, and `game_stats_db`.

The script starts MongoDB and initializes `game_stats_db` only if it does not
exist. PostgreSQL is started only when SQL setup was selected, and each project
database is initialized only if it does not already exist. Missing setup files
are skipped.

 `p1` and `p2` use separate PostgreSQL databases; `p3` uses the MongoDB database `game_stats_db`.

See [the ER diagrams](docs/erd.md) and [database domain/design notes](db/DATABASE.md).
