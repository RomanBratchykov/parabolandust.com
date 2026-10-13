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

Run the initial setup once:

# Accounts service (Lab 02)

This solution implements the Accounts API with PostgreSQL, a Dapper/ADO.NET data layer, and a .NET Aspire AppHost.

## Requirements

- .NET 10 SDK
- Docker Desktop or another Docker-compatible engine
- A NuGet connection for the first package restore

## Run with Aspire

From the repository root, run:

```bash
dotnet run --project Services/Accounts/Accounts.AppHost
```

The AppHost starts a PostgreSQL container and the Accounts API. At first launch, Aspire prompts for the secret `postgres-password`; use `secret` to match the local Lab 01 Compose configuration, or provide a different password. PostgreSQL creates the `accounts_db_p1` database and applies `db/p1/schema.sql`, `db/p1/seed.sql`, and `db/p1/procedures.sql` in that order when its data volume is initialized for the first time.

Open the Aspire Dashboard URL printed in the console to see the API and database resources. In Development, Swagger UI is available at the API resource's `/swagger` path.

To rebuild the solution without starting containers:

```bash
dotnet build Platform.slnx -m:1
```

## Run the API against an existing PostgreSQL instance

The API reads `ConnectionStrings:AccountsDb` from `Services/Accounts/Accounts.Api/appsettings.json`. Override it with the environment variable `ConnectionStrings__AccountsDb`, for example:

```bash
ConnectionStrings__AccountsDb='Host=localhost;Port=5432;Database=accounts_db_p1;Username=postgres;Password=secret' \
  dotnet run --project Services/Accounts/Accounts.Api
```

Apply the Lab 01 SQL files to the target database before using the API. When launching outside Aspire, run `dotnet run` from the repository root if you want the AppHost's SQL-file bind mounts to resolve as written.

## API routes

- `GET /api/users/{userId}`
- `POST /api/users` (optionally grant `initialAchievementId` in the same transaction)
- `PUT /api/users/{userId}`
- `DELETE /api/users/{userId}?expectedRowVersion=1`
- `GET /api/donations/{donationId}`
- `POST /api/donations`
- `PUT /api/donations/{donationId}`
- `DELETE /api/donations/{donationId}`
- `PATCH /api/donations/{donationId}/status`
- `POST /api/user-achievements`

Validation errors return `400`, missing resources return `404`, and state or uniqueness conflicts return `409` as ProblemDetails responses.

### Transaction and concurrency notes

The Accounts Unit of Work uses PostgreSQL `ReadCommitted` isolation by default.
This keeps ordinary reads from seeing uncommitted writes without taking the
stronger locks associated with higher isolation levels. Higher isolation can
reduce anomalies in concurrent workflows, but can increase blocking and
deadlock risk; keep transactions short and retry only operations that are safe
to repeat. Repository commands currently use the provider's default command
timeout. Profile slow queries and set a command timeout explicitly if the
service's workload requires a different limit.

User profile updates and soft deletes use the database `row_version` as an
optimistic concurrency token. A stale version becomes a `409 Conflict`.

### Example requests

Create a user (the response includes a `Location` header):

```bash
curl -i -X POST http://localhost:5000/api/users \
  -H 'Content-Type: application/json' \\
  -d '{"userName":"O’Brien","userEmail":"obrien@example.com","passwordHash":"hash-placeholder","userWhoAmI":"Player"}'
```

Read a user:

```bash
curl -i http://localhost:5000/api/users/1
```

Create a donation:

```bash
curl -i -X POST http://localhost:5000/api/donations \
  -H 'Content-Type: application/json' \\
  -d '{"userId":1,"donationAmount":5.00,"currency":"USD","isAnonymous":false}'
```

Grant an achievement transactionally:

```bash
curl -i -X POST http://localhost:5000/api/user-achievements \
  -H 'Content-Type: application/json' \\
  -d '{"userId":1,"achievementId":1}'
```

Replace `localhost:5000` with the API URL shown in the Aspire Dashboard. The
example user and achievement IDs must exist in the target database.

## Troubleshooting Aspire startup

Aspire needs access to a container runtime. If startup fails during the DCP/orchestrator dependency check, verify that `docker info` works for the same user and terminal that runs the AppHost. A Docker socket `permission denied` error means that user cannot access the Docker daemon; run from a Docker-enabled shell or configure a supported runtime such as Podman (`ASPIRE_CONTAINER_RUNTIME=podman`). On Linux, membership in the `docker` group grants root-level privileges, so only add trusted users to it.

The HTTPS developer-certificate warning does not cause the DCP timeout. Trust the ASP.NET Core development certificate separately if you need browser trust for local HTTPS:

```bash
dotnet dev-certs https --trust
```
