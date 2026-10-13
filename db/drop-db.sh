#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose=(docker compose --project-directory "$script_dir" -f "$script_dir/docker-compose.yml")

if ! command -v docker >/dev/null 2>&1; then
    echo "Error: Docker is required to run this script." >&2
    exit 1
fi

"${compose[@]}" up -d db mongo

printf 'Waiting for PostgreSQL'
for attempt in {1..30}; do
    if "${compose[@]}" exec -T db pg_isready -U postgres -d postgres >/dev/null 2>&1; then
        printf '\n'
        break
    fi
    if (( attempt == 30 )); then
        printf '\nPostgreSQL did not become ready within 30 seconds.\n' >&2
        exit 1
    fi
    printf '.'
    sleep 1
done

printf 'Dropping PostgreSQL database accounts_db_p1 if it exists...\n'
"${compose[@]}" exec -T db psql -v ON_ERROR_STOP=1 -U postgres -d postgres \
    -c 'DROP DATABASE IF EXISTS accounts_db_p1 WITH (FORCE)'

printf 'Dropping PostgreSQL database accounts_db_p2 if it exists...\n'
"${compose[@]}" exec -T db psql -v ON_ERROR_STOP=1 -U postgres -d postgres \
    -c 'DROP DATABASE IF EXISTS accounts_db_p2 WITH (FORCE)'

printf 'Dropping PostgreSQL database mydb if it exists...\n'
"${compose[@]}" exec -T db psql -v ON_ERROR_STOP=1 -U postgres -d postgres \
    -c 'DROP DATABASE IF EXISTS mydb WITH (FORCE)'

printf 'Waiting for MongoDB'
for attempt in {1..30}; do
    if "${compose[@]}" exec -T mongo mongosh --quiet 'mongodb://localhost:27017/admin' \
        --eval 'db.runCommand({ ping: 1 }).ok' >/dev/null 2>&1; then
        printf '\n'
        break
    fi
    if (( attempt == 30 )); then
        printf '\nMongoDB did not become ready within 30 seconds.\n' >&2
        exit 1
    fi
    printf '.'
    sleep 1
done

printf 'Dropping MongoDB database game_stats_db if it exists...\n'
"${compose[@]}" exec -T mongo mongosh --quiet 'mongodb://localhost:27017/admin' \
    --eval 'db.getSiblingDB("game_stats_db").dropDatabase()'

printf 'Databases dropped. Containers and storage volumes were kept.\n'
