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
- ```-d` to drop db and create new
- ```--scheme``` to create scheme

without any parameters script wont do anything
Script will create three databases specified in /db

See [the ER diagrams](docs/erd.md) and [database domain/design notes](db/DATABASE.md).
