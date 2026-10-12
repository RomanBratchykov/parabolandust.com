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

run 
```bash
./db/run-sql.sh
```
with parameters:

- ```-p``` for procedures
- ```-s``` to seed
- ```-d``` o drop db and create new
- ```--scheme``` to create scheme
- ```--all``` to activate all parts

The script always starts the MongoDB service and applies existing `p3/collections.js` and `p3/seed.js` scripts. Missing setup scripts are skipped. SQL services only start when selected SQL files exist; a missing `p1` or `p2` directory or individual `schema.sql`, `procedures.sql`, or `seed.sql` file is skipped.

 `p1` and `p2` use separate PostgreSQL databases; `p3` uses the MongoDB database `game_stats_db`.

See [the ER diagrams](docs/erd.md) and [database domain/design notes](db/DATABASE.md).
