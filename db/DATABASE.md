<h1>DB</h1>
<h2>Subject area</h2>
Site for videogame. Used by people, who wants to check what is this game, look over news, videos, photos, process of development.
Used for:

- Register/Login
- Newsletter
- Stats(Personal and leaderboard)
- Donations(Pre DEMO)
- Community network(Post DEMO)

For Lab 01, the domain is split into three independent database contexts:
- Project 1 — SQL: Accounts and game identity (users, game links, achievements, donations)
- Project 2 — SQL: Newsletter publishing (admins, newsletters, comments, subscriptions)
- Project 3 — MongoDB: Player statistics (player stats, runs, game events)
Each entity has one owning context. Between databases, use logical IDs and documented copies only; do not create cross- database foreign keys.

Flow of website:

User registers - > check newsletter - > check leaderboards - > check his own stats - > can modify his profile

Leaderboard calculated from all projects down below
<h3>User</h3>
Relational DB. 
Splits into few tables:

<h4>users:</h4>
Have next fields:

- user_id(required, primary)
- user_name(required, varchar, unique)
- user_email(required, varchar)
- password_hash
- oauth_provider(unique)
- oauth_id(unique)
- created_at(timestamp)
- user_whoami(required, unique, used to connect into game, varchar, hash)
- user_photo(*_url)
- row_version(bigint, default 1, optimistic concurrency)
- is_deleted(bool, default false, soft delete)

<h4>game_linking:</h4>

*1:1 with users; user_id is both primary key and foreign key to users.user_id.*

Fields:

- user_id(foreign, primary, number)
- connected_at(timestamp)
- removed_at(timestamp)
- connecting_hash(unique)

*A removed link is retained. The user cannot link again after removal, including to another game version.*

<h4>user_achievements:</h4>

*connects user and achievements as N:N(many users - many achievements)*

Fields:

- composite primary key (user_id, achievement_id)
- user_id(foreign to users.user_id)
- achievement_id(foreign to achievement.achievement_id)
- unlocked_at(timestamp)
*Enforce uniqueness on (user_id, achievement_id); preserve earned achievements.*

<h4>achievement:</h4>

- achievement_id(primary)
- achievement_name(varchar)
- achievement_description(varchar)
- is_secret(bool, default false)

*predefined list, could be easily updated(achievement get orchestrated by the game, on website only short description for achievements page, if achievement is secret, it hides name and description, until someone finds it)*

<h4>donations</h4>

*connects user as 1:N(1 user - many donations)*

- donation_id
- user_id(foreign, null)
- donation_amount(numeric)
- commentary(varchar, nullable)
- donated_at(timestamp)
- donation_status
- is_anonymous(bool)
- currency
- provider_payment_id(unique)
*Used for donators leaderboard
*donation_amount must be positive; status and currency have allowed values. Keep payment history and record refunds/failures in status.*



<h3>Stats:</h3>
MongoDB

<h4>stats_user:</h4>

- user_id(unique)
- user_whoami(copy)
- updated_at(timestamp)
- {enemies}:
    - enemies_killed
    - enemies_pursued
    - enemies_forgiven
- {friends}:
    - friends_made
    - friends_killed
    - friends_freed
- {relations}:
    - Meowbert
    - Nyanbert
    - Emma
    - Clodth
    - Arp
    - Dgio
    - Llania
- {totals}:
    - times_killed
    - times_won
    - time_played
- {progress}:
    - challenges completed[]
    - achievements_got
    - combos_collected
    - rooms_visited
- {items}:
    - items_collected
    - items_used
- {commands}:
    - commands_collected
    - commands_used
- {pickups}:
    - pickups_collected
    - keys_used
    - bombs_used
    - wasted_money
    - raised_money
- {events}:
    - secrets_found
    - events_triggered
    - chests_opened

<h4>runs</h4>

- user_id
- run_id(unique)
- {current_stats}
- started_at
- game_version
- ended_at
- successful
*user_id is a logical reference to Project 1. current_stats is a run- time snapshot. Index by (user_id, started_at) for run history.*

<h4>game_events</h4>

- event_id(primary or unique)
- event_type
- user_id(logical reference)
- run_id(logical reference, nullable)
- occurred_at(timestamp)
- event- specific payload fields (may vary by event_type)
*Flexible event documents demonstrate different field sets in one collection. References are logical IDs, not foreign keys.*


<h3>Newsletter:</h3>

<h4>users_copy</h4>

- user_id(primary)
- user_name(varchar)
- user_email(varchar, if required for delivery)
- source_updated_at(timestamp)

*Read copy from Project 1; user_id is a logical ID, not a cross- database foreign key. Update this copy from account- change events; Newsletter does not edit account identity.*

<h4>admins</h4>

- admin_id(primary)
- admin_login(varchar, unique)
- admin_password_hash(hash)
- admin_name(varchar)

<h4>newsletter:</h4>

*main block of newsletter, connects with admins 1:N(1 admin - many newsletters)*
- newsletter_id(primary)
- admin_id(foreign)
- published_at(timestamp)
- title
- main_info(text)
- image(_url)
- is_hidden(bool)

<h4>newsletter_delivery_settings:</h4>

*1:1 dependent record for newsletter; newsletter_id is both primary key and foreign key to newsletter.newsletter_id.*

- newsletter_id(primary, foreign)
- delivery_mode
- scheduled_at(timestamp, nullable)
- recipient_settings
- reply_to_email(nullable)

<h4>user_comments:</h4>

*Each comment refers to one local users_copy row and one newsletter; a newsletter has many comments (1:N).*
- comment_id(primary)
- user_id(foreign)
- comment(text)
- parent_comment_id(foreign to comment_id)
- newsletter_id(foreign)
- is_hidden(bool)


<h4>subscriptions</h4>

*M:N junction between users_copy and newsletter; each subscription belongs to one of each and carries its own status and consent.*
- subscription_id(primary)
- status(enum)
- consent_at(timestamp)
- unsubscribe_token
- user_id(foreign)
- user_email(snapshot, if needed for delivery)
- newsletter_id(foreign)
*Enforce uniqueness on (user_id, newsletter_id), not globally on user_email.*








<h2>Data duplication and Lab 01 requirements</h2>

| Duplicated field | Stored in | Owner/source | Consistency policy |
| --- | --- | --- | --- |
| users_copy.user_id, user_name, and optional user_email | Project 2 SQL | users in Project 1 | Read copy updated by account- change events; Project 2 never edits the source identity. |
| stats_user.user_whoami | Project 3 MongoDB | users.user_whoami in Project 1 | Copy used to match game statistics; refresh when the source identity is rotated. No cross- database FK. |
| user_achievements unlock state and stats_user.progress.achievements_got | Project 1 SQL and Project 3 MongoDB | Game/backend reports unlock events; Project 1 owns achievement descriptions | Both representations update from the same game event; neither is independently edited. |

The two SQL databases must each demonstrate 1:1, 1:N, and M:N using local PK/FK relationships. Include PK, FK, UNIQUE, CHECK, DEFAULT, and query- motivated indexes. Add audit fields, soft deletion where records must be retained, and concurrency tokens where concurrent edits are possible. Project 1 requires 4–6 stored procedures, including a transactional status change that raises a controlled error for an invalid transition. Seed scripts must be repeatable. MongoDB requires 3–4 collections, indexes, embedded data, references, a hybrid document, and examples with different field sets. Deployment instructions and ERDs belong in README.md and docs/; scripts belong under db/p1/, db/p2/, and db/p3/.
