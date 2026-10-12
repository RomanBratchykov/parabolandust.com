#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose=(docker compose --project-directory "$script_dir" -f "$script_dir/docker-compose.yml")
databases=(accounts_db_p1 accounts_db_p2)

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

seed=false
procedures=false
scheme=false
drop=false
success=0
while getopts "spdsc" opt; do
    case $opt in
        s) seed=true ;;
        p) procedures=true ;;
        d) drop=true ;;
        c) scheme=true ;;
        *) echo "Usage: $0 [-s] [-p] [-d] [--scheme]" >&2; exit 1 ;;
    esac
done

files=()

[ "$seed" = true ] && files+=(seed.sql)
[ "$procedures" = true ] && files+=(procedures.sql)
[ "$scheme" = true ] && files+=(schema.sql)



for database in "${databases[@]}"; do
    if [ "$drop" = true ]; then
        "${compose[@]}" exec -T db psql -v ON_ERROR_STOP=1 -U postgres -d postgres \
            -c "DROP DATABASE IF EXISTS \"$database\" WITH (FORCE)"
        "${compose[@]}" exec -T db psql -v ON_ERROR_STOP=1 -U postgres -d postgres \
            -c "CREATE DATABASE \"$database\""
    elif ! "${compose[@]}" exec -T db psql -U postgres -d postgres -tAc \
        "SELECT 1 FROM pg_database WHERE datname = '$database'" | grep -qx '1'; then
        "${compose[@]}" exec -T db createdb -U postgres "$database"
    fi
done

if [ "${#files[@]}" -eq 0 ]; then
    echo "No SQL files specified. Use -s, -p, or -c to specify which files to run." >&2
    exit 1
fi

for project in p1 p2; do
    if [ "$project" = p1 ]; then
        database=${databases[0]}
    else
        database=${databases[1]}
    fi

    for sql_file in "${files[@]}"; do
        printf 'Applying %s/%s to %s...\n' "$project" "$sql_file" "$database"
        "${compose[@]}" exec -T db psql \
            -v ON_ERROR_STOP=1 \
            -U postgres \
            -d "$database" \
            -b \
            -f - < "$script_dir/$project/$sql_file"
        success=$((success + 1))
    done
done

if [ "$success" = 0 ]; then
    printf 'No SQL files were applied.\n'
elif [ "$success" = 1 ]; then
    printf '1 SQL file was applied.\n'
else
    printf '%d SQL files were applied.\n' "$success"
fi
