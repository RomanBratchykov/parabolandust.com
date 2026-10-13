#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose=(docker compose --project-directory "$script_dir" -f "$script_dir/docker-compose.yml")
databases=(accounts_db_p1 accounts_db_p2)
projects=(p1 p2)

seed=false
procedures=false
scheme=false
all=false
while getopts ":spc-a-:" opt; do
    case "$opt" in
        s) seed=true ;;
        p) procedures=true ;;
        c) scheme=true ;;
        a) all=true ;;
        -)
            case "$OPTARG" in
                schema|scheme) scheme=true ;;
                all) all=true ;;
                *) echo "Unknown option: --$OPTARG" >&2; exit 2 ;;
            esac
            ;;
        :|\?) echo "Usage: $0 [-s] [-p] [-c|--schema] [-a|--all]" >&2; exit 2 ;;
    esac
done
shift $((OPTIND - 1))
if (($# > 0)); then
    echo "Unexpected argument: $1" >&2
    exit 2
fi

# This script initializes databases only once. Any selected SQL component
# therefore needs the schema on that same first run.
if [[ "$all" == true || "$procedures" == true || "$seed" == true ]]; then
    scheme=true
fi

if ! command -v docker >/dev/null 2>&1; then
    echo "Error: Docker is required to run this script." >&2
    exit 1
fi

"${compose[@]}" up -d mongo
printf 'Waiting for MongoDB'
for attempt in {1..30}; do
    if "${compose[@]}" exec -T mongo mongosh --quiet 'mongodb://localhost:27017/game_stats_db' \
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

mongo_applied=0
mongo_db_exists=$("${compose[@]}" exec -T mongo mongosh --quiet 'mongodb://localhost:27017/admin' \
    --eval 'db.adminCommand({ listDatabases: 1, nameOnly: true }).databases.some(database => database.name === "game_stats_db")')
if [[ "$mongo_db_exists" == "true" ]]; then
    printf 'Skipping existing MongoDB database game_stats_db; it was left unchanged.\n'
    mongo_scripts_enabled=false
else
    mongo_scripts_enabled=true
fi

for mongo_script in collections.js seed.js; do
    [[ "$mongo_scripts_enabled" == true ]] || break
    path="$script_dir/p3/$mongo_script"
    if [[ ! -f "$path" ]]; then
        printf 'Skipping missing p3/%s.\n' "$mongo_script"
        continue
    fi
    printf 'Applying p3/%s to game_stats_db...\n' "$mongo_script"
    "${compose[@]}" exec -T mongo mongosh --quiet 'mongodb://localhost:27017/game_stats_db' \
        --file "/scripts/$mongo_script"
    mongo_applied=$((mongo_applied + 1))
done
if (( mongo_applied == 0 )) && [[ "$mongo_scripts_enabled" == true ]]; then
    printf 'MongoDB is running; no p3 setup scripts were found.\n'
fi

sql_files=()
[[ "$scheme" == true ]] && sql_files+=(schema.sql)
[[ "$procedures" == true ]] && sql_files+=(procedures.sql)
[[ "$seed" == true ]] && sql_files+=(seed.sql)
[[ "$all" == true ]] && sql_files+=(schema.sql procedures.sql seed.sql)

available_projects=()
for index in "${!projects[@]}"; do
    project="${projects[$index]}"
    project_has_file=false
    for sql_file in "${sql_files[@]}"; do
        if [[ -f "$script_dir/$project/$sql_file" ]]; then
            project_has_file=true
        else
            printf 'Skipping missing %s/%s.\n' "$project" "$sql_file"
        fi
    done
    if [[ "$project_has_file" == true ]]; then
        available_projects+=("$index")
    fi
done

if ((${#sql_files[@]} == 0)); then
    printf 'No SQL files selected. Use -s, -p, -c/--schema, or -a/--all to apply SQL files.\n'
elif ((${#available_projects[@]} == 0)); then
    printf 'No requested SQL files were found; PostgreSQL was not started.\n'
else
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

    sql_applied=0
    for index in "${available_projects[@]}"; do
        project="${projects[$index]}"
        database="${databases[$index]}"

        if "${compose[@]}" exec -T db psql -U postgres -d postgres -tAc \
            "SELECT 1 FROM pg_database WHERE datname = '$database'" | grep -qx '1'; then
            printf 'Skipping existing PostgreSQL database %s; it was left unchanged.\n' "$database"
            continue
        else
            "${compose[@]}" exec -T db createdb -U postgres "$database"
        fi

        for sql_file in "${sql_files[@]}"; do
            path="$script_dir/$project/$sql_file"
            [[ -f "$path" ]] || continue
            printf 'Initializing new database %s with %s/%s...\n' "$database" "$project" "$sql_file"
            "${compose[@]}" exec -T db psql \
                -v ON_ERROR_STOP=1 \
                -U postgres \
                -d "$database" \
                -b \
                -f - < "$path"
            sql_applied=$((sql_applied + 1))
        done
    done

    if (( sql_applied == 0 )); then
        printf 'No new SQL databases were initialized; existing databases were left unchanged.\n'
    elif (( sql_applied == 1 )); then
        printf '1 SQL file was applied.\n'
    else
        printf '%d SQL files were applied.\n' "$sql_applied"
    fi
fi
