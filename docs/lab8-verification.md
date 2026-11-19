# Lab 8 runtime verification

Use disposable lab databases and fixture accounts. Keep the existing resource
volumes, credentials, replica-set name, and queue declarations. Record actual
responses, EventIds, versions, queue counts, and screenshots; the expected results
below are a procedure, not evidence that these live checks have passed.

## 1. Start and inspect the platform

Follow [AppHost setup](../README.md#integrated-platform-lab-5), then run from the
repository root:

```powershell
dotnet restore Platform.slnx
dotnet build Platform.slnx --no-restore -m:1
dotnet run --project Platform.AppHost
```

Docker must be running. Open the Aspire Dashboard URL printed by AppHost and use
the actual API, database, broker AMQP, and Management endpoints shown there.
AppHost passes credentials through secret parameters and connection strings.
For standalone APIs, provide their database connection, Redis, and
`ConnectionStrings__messaging`; GameStats must connect to a replica set with
transactions enabled. Preserve authentication, TLS, direct-connection, and
replica-set parameters in the generated MongoDB URI.

On a new PostgreSQL volume, AppHost bootstraps `accounts_db_p1` and creates
`accounts_db_p2`. Newsletters applies EF migrations, including its inbox upgrade,
before the host starts the consumer. Existing volumes are not reinitialized.
MongoDB's `stats_user` unique user index and inbox are created on event processing.
To use the Lab 4 validators and run/event collections on a fresh lab MongoDB,
apply `db/p3/collections.js` with `mongosh` connected to `game_stats_db`.
AppHost does not run the Mongo seed scripts automatically.

Check `/health/ready` on all APIs and `/health/live` separately. Each ready body
must show a healthy business database and `messaging`. Redis can report
`Degraded` with HTTP 200; inspect the JSON. A Mongo ping does not prove transaction
capability: execute the opt-in Mongo tests below. In Management, verify:

| Exchange | Type | Bindings |
| --- | --- | --- |
| `account-events` | durable topic | `account.*` to both main queues; `achievement.*` to GameStats |
| `account-events.dlx` | durable direct | each source queue name to its own `.dlq` |

Both main queues and both DLQs must be durable, nonexclusive, and non-autodeleting.
Main queues must have the expected `x-dead-letter-exchange` and queue-specific
`x-dead-letter-routing-key`. Save a Management topology screenshot. Do not delete
queues to resolve an incompatible declaration; inspect and plan that change
separately, since queued messages may be present.

## 2. Capture one original event and create fixtures

After the stack is healthy, stop only `newsletters-api` and `gamestats-api` in
Aspire. Keep Accounts, the broker, and Gateway running. Create a unique account:

```powershell
$labBase = 'http://localhost:5000'
$labSuffix = [Guid]::NewGuid().ToString('N')
$labCorrelation = 'lab8-' + $labSuffix
$labHeaders = @{ 'X-Correlation-Id' = $labCorrelation }
$labCreate = @{
    userName = 'lab8_' + $labSuffix.Substring(0, 12)
    userEmail = $labSuffix + '@example.test'
    passwordHash = 'lab8-fixture-hash'
    userWhoAmI = 'lab8-' + $labSuffix
} | ConvertTo-Json
$labCreated = Invoke-RestMethod "$labBase/api/users" -Method Post -Headers $labHeaders -ContentType 'application/json' -Body $labCreate
$labUserId = [long]$labCreated.userId
$labAccount = Invoke-RestMethod "$labBase/api/users/$labUserId"
```

The source account must exist and both main queues must retain its creation
event. In Management, inspect a queued message with requeue enabled, retaining
its exact JSON payload, `type`, `message_id`, and headers. Do not acknowledge or
purge the only copy. This saved original envelope will be reused for duplicate
and stale-event checks. The body must omit password/OAuth fields.

Restart both consumers. Poll until `newsletter.users_copy` and `stats_user` contain
the fixture ID, then record each inbox entry for the captured EventId. There must
be one row/document per consuming service. Use the separate stores:

```sql
-- accounts_db_p1: substitute the numeric fixture ID.
SELECT user_id, user_name, user_email, user_whoami, created_at, updated_at,
       row_version, is_deleted FROM accounts.users WHERE user_id = <fixture-id>;
-- accounts_db_p2:
SELECT user_id, user_name, user_email, source_version, source_updated_at,
       row_version, is_deleted FROM newsletter.users_copy WHERE user_id = <fixture-id>;
SELECT event_id, processed_at FROM newsletter.processed_events WHERE event_id = '<event-id>';
```

In `mongosh`, select `game_stats_db`, substitute the fixture ID, and inspect:

```javascript
const labUserId = NumberLong("<fixture-id>");
db.stats_user.findOne({ user_id: labUserId });
db.processed_events.findOne({ _id: "<event-id>" });
```

For snapshot checks, subscribe this fixture account to an existing published lab
newsletter with `POST /api/subscriptions` (`userId`, `newsletterId`). Record its
email snapshot, consent, status, unsubscribe token, and row version. Insert a
valid fixture run into the disposable MongoDB using the Lab 4 shape: a unique
`run_id`, this Int64 `user_id`, `started_at`, `game_version`, `current_stats`, and
`player_snapshot` with the original account name/identity. Record the whole run.
Set nonzero `stats_user.totals` and an unrelated progress field for this fixture
using the existing BSON types, so a reset cannot pass unnoticed.

## 3. Profile, rotation, unlock, and deletion

Use the latest `rowVersion` before each account write:

```powershell
$labAccount = Invoke-RestMethod "$labBase/api/users/$labUserId"
$labUpdate = @{
    userName = 'edited_' + $labSuffix.Substring(0, 12)
    userEmail = $labSuffix + '@edited.example.test'
    expectedRowVersion = $labAccount.rowVersion
} | ConvertTo-Json
Invoke-RestMethod "$labBase/api/users/$labUserId" -Method Put -Headers $labHeaders -ContentType 'application/json' -Body $labUpdate
$labAccount = Invoke-RestMethod "$labBase/api/users/$labUserId"
$labRotate = @{
    userWhoAmI = 'rotated-' + $labSuffix
    expectedRowVersion = $labAccount.rowVersion
} | ConvertTo-Json
Invoke-RestMethod "$labBase/api/users/$labUserId/identity" -Method Put -Headers $labHeaders -ContentType 'application/json' -Body $labRotate
```

After propagation, compare source versions/timestamps with Newsletters and the
current Mongo identity. BSON dates have millisecond precision. The subscription
snapshot, run snapshot/current stats, gameplay totals, and unrelated progress
must match their saved values. A repeated stale-version rotation must return 409,
without another committed source change or event.

Choose an existing achievement ID from `accounts.achievement`. POST
`{ "userId": <fixture-id>, "achievementId": <achievement-id> }` to
`/api/user-achievements`. Check the stored PostgreSQL `unlocked_at`, event
timestamp, and exactly one Int64 achievement ID in MongoDB. Repeat the grant:
expect 409 and no new event. An unlock must not change the account profile version
or remove existing achievement strings/IDs. Separately create another disposable
account with `initialAchievementId`; both its account and unlock events must
appear only after the source transaction commits. A nonexistent achievement
must return 404 and leave no newly created account or published event.

Replay the saved creation event twice through `account-events` with routing key
`account.created`, preserving its `type`, `message_id`, JSON `eventId`, timestamps,
and schema version. Keep delivery mode persistent. Neither projection may revert,
and each service must still have one inbox entry for that original EventId.

Finally GET the latest account version and DELETE
`/api/users/<fixture-id>?expectedRowVersion=<version>`. After propagation, both
projections must retain deletion tombstones at the newer source version. Replay
the old creation envelope again: it must not resurrect either copy. The direct
statistics reader still retains historical statistics; the required Accounts read
returns 404. Confirm subscription/run snapshots and totals remain unchanged.

## 4. Outages, retry isolation, DLQ, and traces

Run each scenario on the disposable fixture, recording before/after counts and
timing. Do not reset shared volumes, purge queues, or change broker declarations.

| Scenario | Expected observation |
| --- | --- |
| Stop one consumer, perform several account writes, restart it | Its durable queue drains and latest source versions appear; the other consumer progresses independently. |
| Restart the broker with queued messages while consumers are stopped | Named broker volume retains the topology/backlog; restart consumers and compare inboxes/projections. |
| Stop a consuming database, then publish one valid event | With uninterrupted broker transport, initial attempt plus at most three queue-local retries; recovery before exhaustion commits once. After exhaustion, only that consumer's DLQ increases. Restore DB and verify readiness/recovery. |
| Break the broker connection during retry publication | Original is not acknowledged before a confirmed retry; the worker recreates its channel and redelivery is deduplicated after recovery. Correlate transport logs and inbox state. |
| Cancel a broker subscription with its channel otherwise open | Consumer readiness becomes unhealthy and the worker resubscribes; liveness stays healthy. |
| Stop a consumer during an in-flight write | The delivery remains unacknowledged unless processing completed; after restart, inspect atomic inbox/projection state and duplicate handling. |
| Send malformed JSON directly to the default exchange with routing key `newsletters.account-sync` | Set `type=AccountProfileChangedEvent` and a fresh `message_id`. Only the Newsletters DLQ increases; GameStats is unaffected. Save the DLQ screenshot. |

The one-second retry delay is additional to database/connection timeouts. It is
not an overall processing deadline. Transport redelivery can repeat an attempt
without advancing its retry header. For an exhaustion demonstration, keep the
database stopped and broker transport healthy until the DLQ entry appears.
For recovery, save that entry's payload/properties before any removal. After
fixing the cause, republish only to the failed subscription via the default
exchange and its exact queue name. Preserve EventId/type/schema/correlation;
reset `x-retry-count` to zero for an intentional new retry budget. Confirm success
before acknowledging/removing the saved DLQ entry. Replaying through the source
exchange would fan out to already successful consumers. There is no automatic
DLQ replay, outbox recovery, or guarantee against failed default DLX forwarding.

Filter Aspire traces by `$labCorrelation`/the HTTP trace ID and make a fresh
profile or identity request. Capture a trace with HTTP, `account-events publish`,
both queue `process` activities, and their SQL/Mongo writes. Confirm a common trace
ID and correct parent links, including across a retry. Cache invalidations follow
successful projection commits. Logs must show EventId, queue, type, correlation,
UserId, and AchievementId for unlocks, plus exceptions for failures. Inspect main
queue/DLQ depth in RabbitMQ Management; a DLQ gauge is not implemented.

Save three required screenshots locally: broker exchanges/queues/bindings, an
exported HTTP → publish → both consumers → databases trace, and a DLQ message.
Use disposable fixture data and avoid exposing broker credentials in screenshots.

## 5. Automated checks and evidence status

```powershell
dotnet build Platform.slnx --no-restore -m:1
dotnet test Platform.slnx --no-restore -m:1
```

Enable live tests only against disposable stores, then rerun the same command:

| Environment variable | Required target |
| --- | --- |
| `ACCOUNTS_TEST_CONNECTION` | PostgreSQL database initialized with `db/p1/schema.sql` and `procedures.sql` |
| `NEWSLETTERS_TEST_CONNECTION` | Disposable PostgreSQL database; tests apply EF migrations |
| `GAMESTATS_TEST_CONNECTION` | MongoDB URI with a database name and replica-set transactions; tests create/drop uniquely named `lab8_test_*` databases and require permission to do so |
| `NEWSLETTERS_TEST_URL` | Running test Newsletters API against the disposable database, including its Redis/broker configuration |
| `NEWSLETTERS_TEST_LOG` | That API's structured console log, used with the URL for exception-log verification |

The whole-change review of `[83]`–`[93]` found no demonstrated critical or important
defect. Its entity-ID logging finding was fixed with failing-then-passing tests.
The opt-in PostgreSQL snapshot fixture was also corrected to use the seeded admin
and the newsletter's database-generated ID. These fixture fixes do not constitute
a live database test pass.

Final verification for `[94]`: the full solution build passed with existing
package/CLI warnings; 176 tests passed and 25 integration checks were skipped
across all five test projects. Local documentation links and `git diff --check`
also passed. Build/test logs are retained outside Git in the session's temporary
directory. Live broker restart, outage recovery, database transaction checks, exported OTLP
traces, and screenshots remain pending: the available environment has no native
Docker CLI, and the configured Debian WSL disk is missing. Do not report full Lab
8 runtime acceptance until the recorded live procedure has passed.
