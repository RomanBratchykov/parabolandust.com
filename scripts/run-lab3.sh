#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/.." && pwd)"
compose_file="$repository_root/db/docker-compose.yml"
postgres_container="pg-test"
test_database="newsletters_lab3_test"
application_database="newsletters_lab3"
postgres_password="secret"

nuget_windows_path='C:\Users\Roman\AppData\Local\Microsoft\WinGet\Links\nuget.exe'
if command -v cygpath >/dev/null 2>&1; then
    nuget_exe="$(cygpath -u "$nuget_windows_path")"
elif [[ -x /mnt/c/Users/Roman/AppData/Local/Microsoft/WinGet/Links/nuget.exe ]]; then
    nuget_exe=/mnt/c/Users/Roman/AppData/Local/Microsoft/WinGet/Links/nuget.exe
elif [[ -x /c/Users/Roman/AppData/Local/Microsoft/WinGet/Links/nuget.exe ]]; then
    nuget_exe=/c/Users/Roman/AppData/Local/Microsoft/WinGet/Links/nuget.exe
else
    nuget_exe="$nuget_windows_path"
fi
export NUGET_EXE="$nuget_exe"
if [[ -x "$nuget_exe" ]]; then
    export PATH="$(dirname "$nuget_exe"):$PATH"
fi

test_connection="Host=localhost;Port=5432;Database=$test_database;Username=postgres;Password=$postgres_password"
application_connection="Host=localhost;Port=5432;Database=$application_database;Username=postgres;Password=$postgres_password"

ensure_database() {
    local database_name="$1"
    local exists

    exists="$(docker exec "$postgres_container" psql -U postgres -tAc \
        "SELECT 1 FROM pg_database WHERE datname = '$database_name'")"
    if [[ "$exists" != "1" ]]; then
        docker exec "$postgres_container" createdb -U postgres "$database_name"
    fi
}

cd "$repository_root"
docker compose -f "$compose_file" up -d db

ready=false
for _ in {1..30}; do
    if docker exec "$postgres_container" pg_isready -U postgres >/dev/null 2>&1; then
        ready=true
        break
    fi
    sleep 1
done
if [[ "$ready" != true ]]; then
    echo "PostgreSQL did not become ready in the '$postgres_container' container." >&2
    exit 1
fi

ensure_database "$test_database"
ensure_database "$application_database"

export NEWSLETTERS_TEST_CONNECTION="$test_connection"
dotnet test Services/Newsletters/Newsletters.Tests/Newsletters.Tests.csproj

export ConnectionStrings__NewslettersDb="$application_connection"
dotnet run --project Services/Newsletters/Newsletters.Api/Newsletters.Api.csproj
