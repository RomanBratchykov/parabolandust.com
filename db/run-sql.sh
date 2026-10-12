#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose=(docker compose --project-directory "$script_dir" -f "$script_dir/docker-compose.yml")
database="accounts_db"

if ! command -v docker >/dev/null 2>&1; then
    echo "Error: Docker is required to run this script." >&2
    exit 1
fi

"${compose[@]}" up -d db

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

if ! "${compose[@]}" exec -T db psql -U postgres -d postgres -tAc \
    "SELECT 1 FROM pg_database WHERE datname = '$database'" | grep -qx '1'; then
    "${compose[@]}" exec -T db createdb -U postgres "$database"
fi

for sql_file in schema.sql procedures.sql seed.sql; do
    printf 'Applying p1/%s to %s...\n' "$sql_file" "$database"
    "${compose[@]}" exec -T db psql \
        -v ON_ERROR_STOP=1 \
        -U postgres \
        -d "$database" \
        -b \
        -f - < "$script_dir/p1/$sql_file"  
done

printf 'Project 1 database is ready.\n'
