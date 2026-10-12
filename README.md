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

Run the initial setup once:
```bash
./db/run-sql.sh --all
```

The script creates and initializes each database only when that database does
not already exist. On later runs it skips existing databases without executing
their schema, procedure, seed, or index scripts, so existing data and indexes
are left untouched. The script does not provide a database drop/reset option.
To rebuild from scratch, remove the relevant database yourself after backing
up any data, then run the setup command again.

For a new database, the optional selectors are:

- ```-p``` for procedures
- ```-s``` to seed
- ```--scheme``` (or ```-c```) to create the schema
- ```--all``` to apply schema, procedures, and seed data

Choose the required selectors on the first run for a database. Once a database
exists, the script intentionally skips it; it is an initializer, not a migration
runner.

To drop the three Lab 01 databases while keeping the containers and volumes,
run:

```bash
./db/drop-db.sh
```

This removes `accounts_db_p1`, `accounts_db_p2`, and `game_stats_db` only.

The script starts MongoDB and initializes `game_stats_db` only if it does not
exist. PostgreSQL is started only when SQL setup was selected, and each project
database is initialized only if it does not already exist. Missing setup files
are skipped.

 `p1` and `p2` use separate PostgreSQL databases; `p3` uses the MongoDB database `game_stats_db`.

See [the ER diagrams](docs/erd.md) and [database domain/design notes](db/DATABASE.md).
