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

## Newsletter service (Lab 3)

The Newsletters service implements Lab 3 with EF Core Code-First and PostgreSQL.
It uses its own `newsletters_lab3` database, separate from the Lab 1 databases.

### Run the lab

Requirements: .NET 10 SDK, Docker Desktop (or a compatible Docker engine), and
PowerShell or Bash (Git Bash, WSL, or Linux). From the repository root, run one
of these:

```powershell
./scripts/run-lab3.ps1
```

```bash
bash ./scripts/run-lab3.sh
```

The script starts the PostgreSQL service from `db/docker-compose.yml`, creates
the `newsletters_lab3` application database and a separate
`newsletters_lab3_test` database if they do not exist, runs the Newsletters
tests, then starts the API. The API applies EF Core migrations at startup and
adds its sample newsletter, delivery settings, and subscription in Development.
Fixed admin and user-copy rows are migration seeds. Re-running the script keeps
the databases and seed data in the existing PostgreSQL container. Removing or
recreating that container resets its data. Stop the API with `Ctrl+C`. The
runners detect Windows or Linux before starting. .NET build outputs use separate
`bin/windows` / `obj/windows` and `bin/linux` / `obj/linux` folders, so Windows
and WSL/Linux builds do not reuse each other's intermediate files.

The runners also invoke `scripts/modules/newsletter-checks.sh` (Bash) or
`scripts/modules/NewsletterChecks.psm1` (PowerShell). These start a temporary API
against `newsletters_lab3_test`, capture its console output in a temporary log
file, and run all seven HTTP checks plus the structured logging check. The API
is stopped after the checks, including on failure. Log paths are printed and
the files are kept for inspection. A failed check stops the runner before it
launches the application API. The HTTP checks add sample rows to the test database.
The check API uses `http://127.0.0.1:5187`; set `NEWSLETTERS_CHECK_URL` to another
free localhost URL if needed. Bash checks require `curl` and use the platform's
build output (`linux` on WSL/Linux, `windows` on Git Bash).

### Create a service scaffold

Run the cross-platform PowerShell script with a service name made of letters
and digits, starting with a letter. PowerShell 7 (`pwsh`) is required on Linux.
It detects Windows or Linux, then creates `Domain`, `Dal`, `Bll`, and `Api`
projects under `Services/<Name>` and adds them to `Platform.slnx`:

```powershell
./scripts/new-service.ps1 Payments
```

The script uses the local Compose credentials (`postgres` / `secret`) on port
5432. To use another PostgreSQL instance, set `ConnectionStrings__NewslettersDb`
and start the API directly:

```powershell
$env:ConnectionStrings__NewslettersDb = "Host=localhost;Port=5432;Database=newsletters_lab3;Username=postgres;Password=secret"
dotnet run --project Services/Newsletters/Newsletters.Api
```

To apply migrations manually or regenerate the SQL migration script, install
the EF tool and run these commands from the repository root:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.4
$env:ConnectionStrings__NewslettersDb = "Host=localhost;Port=5432;Database=newsletters_lab3;Username=postgres;Password=secret"
dotnet ef database update --project Services/Newsletters/Newsletters.Dal --startup-project Services/Newsletters/Newsletters.Api
dotnet ef migrations script --project Services/Newsletters/Newsletters.Dal --startup-project Services/Newsletters/Newsletters.Api --output Services/Newsletters/Newsletters.Dal/Migrations/InitialCreate.sql
```

Swagger is available at `/swagger` in Development. The API routes are:

- `GET /api/newsletters?page=2&pageSize=5&search=update&sortBy=publishedAt&sortDir=desc`
- `GET /api/newsletters/{id}` and `GET /api/newsletters/{id}/details`
- `POST /api/newsletters` with `adminId`, `title`, `mainInfo`, and optional `imageUrl`
- `POST /api/newsletters/{id}/publish` with `publishedBy`
- `GET /api/newsletters/{id}/subscribers?since=2026-01-01T00:00:00Z`
- `GET /api/users-copy/{userId}/subscriptions` (explicit loading)
- `POST /api/subscriptions` with `userId` and `newsletterId`; a duplicate pair returns 409
- `GET /api/subscriptions/{id}`

Filtering, sorting, count, and pagination are applied by EF Core before
materialization. EF command logging is enabled at Information level so a
newsletter details request can be checked for one eager-loading SELECT, while
the user-copy subscriptions request shows the base query and explicit-load
SELECT. Serilog writes structured console logs with `CorrelationId`, `UserId`,
and `RequestPath`. Set `AutoMapper__LicenseKey` if a licensed key is available;
AutoMapper logs a warning when no key is configured.

## Integrated platform (Lab 5)

Requires the .NET 10 SDK, Docker with a running daemon, and NuGet access for
the first restore. Set the local database secrets once (replace the placeholders
with your own values; do not commit them):

```powershell
dotnet user-secrets set "Parameters:postgres-password" "<postgres-password>" --project Platform.AppHost
dotnet user-secrets set "Parameters:mongo-password" "<mongo-password>" --project Platform.AppHost
dotnet run --project Platform.AppHost
```

Open the Dashboard URL printed by AppHost. It starts persistent PostgreSQL,
MongoDB, and Redis, then `accounts-api`, `newsletters-api`, `gamestats-api`,
`aggregator`, and `gateway`. The public Gateway listens on `http://localhost:5000`.
Each HTTP process exposes `/health` (readiness) and `/alive` (liveness).
In Development, each process also exposes `/swagger`.

The connection names are `AccountsDb`, `NewslettersDb`, `GameStatsDb`, and `redis`.
Accounts owns PostgreSQL `accounts_db_p1`; Newsletters owns PostgreSQL
`accounts_db_p2`; GameStats reads MongoDB `game_stats_db`.
The platform retains this repository's PostgreSQL services and game-domain
routes in place of the lab's SQL Server and Orders/Catalog/Reviews examples.
On a fresh PostgreSQL volume, bootstrap creates both SQL databases and applies
the existing Accounts schema, seeds, and procedures. Newsletters applies its
own EF migrations. Restarting AppHost preserves database volumes; bootstrap
does not rerun against an existing volume.

Gateway forwards these paths without changing HTTP methods or backend paths:

| Paths | Destination |
| --- | --- |
| `/api/users`, `/api/donations`, `/api/user-achievements` and subpaths | Accounts |
| `/api/newsletters`, `/api/subscriptions`, `/api/users-copy` and subpaths | Newsletters |
| `/api/stats` and subpaths | GameStats |
| `/api/aggregator` and subpaths | Aggregator |

Request a dashboard for an existing account:

```bash
curl -i -H 'X-Correlation-Id: lab6-demo' http://localhost:5000/api/aggregator/users/1001/dashboard
```

The aggregator first loads the account, then fetches subscriptions and statistics
concurrently. A successful response includes `player`, available `subscriptions`
and `statistics`, and `warnings`. Unavailable optional components are omitted
and produce warnings; a successful empty subscription list remains `[]`.
Missing accounts return 404; Accounts failures return 503 or 504 on timeout;
nonpositive user IDs return 400. Responses use camelCase and omit null fields.
No password hashes, email addresses, or authentication fields enter this DTO.
`Downstream:TimeoutSeconds` controls each downstream call's total budget,
using a gRPC deadline (default 5, supported range 1–30). Generated gRPC clients
retain discovery and correlation, remove HTTP resilience retries, and do not
add a second retry layer.

For the failure demonstration, stop GameStats or Newsletters in Dashboard,
repeat the dashboard request, and inspect its warnings and remaining data.
Restart the service and repeat to observe recovery. GameStats is a read API;
statistics must already exist for the account to get a full dashboard.
Use the existing Accounts CRUD and Newsletters creation/subscription routes
through Gateway to check writes as well as reads.

Find the request in Dashboard Traces to inspect Gateway → Aggregator → services
and database spans, including the two overlapping optional calls. Filter
Structured logs by the response's `X-Correlation-Id`; supplied IDs are forwarded,
and missing IDs are generated even for direct service requests. Inspect service,
environment, machine, trace/span IDs, routing decisions, latency, and failures.
Dashboard Metrics exposes HTTP throughput, duration, and errors. Keep resource,
trace, and filtered-log screenshots locally for submission.

Services can also run independently with `dotnet run --project <project-path>`.
Set `ConnectionStrings__AccountsDb`, `ConnectionStrings__NewslettersDb`, or
`ConnectionStrings__GameStatsDb` as appropriate. For standalone Aggregator or
Gateway, configure logical endpoints such as
`services__accounts-api__http__0=http://localhost:<accounts-port>` for each
referenced backend. OTLP export is enabled when `OTEL_EXPORTER_OTLP_ENDPOINT`
is configured; AppHost supplies discovery and telemetry settings automatically.

## Lab 6 cache behavior

Accounts, Newsletters, and GameStats share the manual cache-aside mechanism:
bounded process memory (L1), Redis (L2), then the existing database reader.
REST and gRPC call the same cached business operations. Nulls, missing data,
exceptions, and canceled loads are not cached. Successful empty subscription
lists are cached.

| Read | L1 sliding / absolute | Redis absolute |
| --- | --- | --- |
| Account profile | 40 s / 45 s | 60 s |
| Newsletter card, details, normalized list | 60 s / 120 s | 300 s |
| User subscriptions | 40 s / 45 s | 60 s |
| Player statistics | 40 s / 45 s | 60 s |
| Complete player dashboard | none | 30 s |

The keyed `Platform.Cache` memory cache has a 10,000-entry limit and every
entry has size 1. The ordinary framework memory cache remains available for
unsized framework entries. L2 promotion caps L1 at the payload's remaining
lifetime. Entry and approximate serialized-byte gauges describe local entries,
not Redis server memory usage.

Keys are `platform:v1:<domain>:<generation>:<operation>:<identity>`.
Newsletter list identities hash all normalized filter, ordering, and pagination
parameters; keys contain no names or email addresses. Redis generation markers
last 24 hours. Successful account create/update/delete, newsletter create/publish,
and subscription creation await domain and dashboard invalidation. Canceled local
tokens prevent an old in-flight read from repopulating a usable L1 entry; old L2
fills stay under their captured generation. Dashboard fills recheck the shared
generation and store only complete, consistent responses. Partial dashboards and
404/503/504 results are never stored. Correlation headers are generated for each
request and are never cached with a dashboard.

For standalone services, provide Redis in addition to the business database:

```powershell
$env:ConnectionStrings__redis = "localhost:6379,abortConnect=false"
```

`/health/cache` on each cache-enabled API and Aggregator returns JSON status
and per-domain `entries`, `bytes`, `hits`, `misses`, and `invalidations`.
`/health` is readiness and returns 503 when Redis is unavailable; `/alive`
remains liveness. Generation gate waits and Redis operations use bounded budgets.
After startup, Redis failures fall back to the existing source readers. Inspect
cache logs and the source spans to distinguish fallback from an L1 or L2 hit.

Database commits and Redis invalidation are not atomic. After a committed write,
invalidation uses its own bounded token, clears local generations even if Redis
fails, marks affected groups dirty, and retries marker advancement on a later
read before trusting L2 again. This does not guarantee immediate coherence across
processes during an outage. Another replica's existing L1 entry can remain until
its short TTL; durable events and Redis pub/sub are outside this lab.
GameStats has no owning write API: external MongoDB updates become visible within
the 60-second service TTL, potentially plus a 30-second dashboard lifetime.

To demonstrate recovery, warm a known record, stop only the Redis resource in
Aspire, and make a cold valid read. Check that readiness fails while liveness
and source fallback remain functional; restart Redis and repeat. A new service
process with empty L1 should obtain an L2 hit. Persistent Redis data survives a
resource restart. Never flush shared Redis or reset business database volumes
for a check.

## Lab 6 gRPC startup and verification

Use `dotnet dev-certs https --check --trust` to check the existing local
development certificate; if needed, create/trust it with
`dotnet dev-certs https --trust`. Start `Platform.AppHost` as above.
Each business API has a separate named `https` endpoint with HTTP/2 support;
HTTP health probes and the public Gateway port 5000 remain available.
Aggregator resolves these logical HTTPS names through service discovery:

| Backend | RPC |
| --- | --- |
| `https://accounts-api` | `accounts.v1.AccountsReader/GetUser` |
| `https://newsletters-api` | `newsletters.v1.NewslettersReader/GetUserSubscriptions` |
| `https://gamestats-api` | `gamestats.v1.GameStatsReader/GetStatistics` |

Standalone Aggregator also needs `ConnectionStrings__redis` and HTTPS discovery
settings for all three backends, for example:

```powershell
Set-Item 'Env:services__accounts-api__https__0' 'https://localhost:<accounts-https-port>'
Set-Item 'Env:services__newsletters-api__https__0' 'https://localhost:<newsletters-https-port>'
Set-Item 'Env:services__gamestats-api__https__0' 'https://localhost:<gamestats-https-port>'
```

Contracts use stable field numbers, int64 identities/play time, nullable photo
presence, and UTC protobuf timestamps. API adapters call the same business readers
as REST. Invalid IDs map to `InvalidArgument`, missing records to `NotFound`,
caller cancellation to `Cancelled`, and unexpected failures to sanitized
`Internal`. Aggregator preserves its HTTP 400/404/503/504 and partial-200 contract.

`Platform.Cache` exports hit/miss counters tagged by domain and L1/L2, operation
duration, invalidations, evictions, local entries, and serialized bytes.
`Platform.Grpc` and `Platform.Grpc.Client` activities include RPC attributes;
structured server logs include method, peer, status, duration, and exceptions.
`Transport=http|grpc`, correlation IDs, and trace IDs distinguish shared-cache
calls without logging payloads or credentials.

Verification on isolated PostgreSQL/MongoDB/Redis stores included 29 HTTPS REST
requests, nine real HTTP/2 gRPC calls across the production APIs, mutation
invalidation and stale-version conflicts, L2 reuse after process restart, and
Redis outage/recovery with working liveness and cold Mongo reads. Temporary
checks additionally covered concurrent cache budgets, stale-fill races,
complete-only dashboard caching, deadlines, cancellation, malformed timestamps,
identity rejection, correlation isolation, and parallel optional calls. A
temporary incompatible protobuf decoder demonstrated that changing the username
field number loses that field; production field numbers remain unchanged.

An equivalent account-first, then two-parallel-call comparison used the same
warm service caches and fixture, without aggregate caching, after five warmups:

| Transport | Requests / errors | Median | p95 |
| --- | --- | --- | --- |
| REST HTTP/1.1 over TLS | 100 / 0 | 11.04 ms | 13.05 ms |
| gRPC HTTP/2 over TLS | 100 / 0 | 13.42 ms | 16.73 ms |

gRPC was slower in this local run; no protocol speedup is claimed. Final build
and test logs, comparison data, and temporary fixtures stay outside Git.

Submission evidence still to capture in the actual Aspire Dashboard: a cold
Gateway-to-databases trace with all three RPCs, a repeated read without SQL,
and nonzero L1/L2 cache metrics. Save the three required screenshots locally.
Actual AppHost orchestration/export screenshots and the complete live aggregate
outage matrix were not captured in this verification; model checks and loopback
tests do not replace that evidence.
