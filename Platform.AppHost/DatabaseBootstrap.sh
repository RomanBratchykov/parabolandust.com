#!/bin/bash
set -euo pipefail
export PGPASSWORD="$POSTGRES_PASSWORD"

# The image runs this only for a new data volume, before accepting TCP connections.
psql --username "$POSTGRES_USER" --dbname postgres --set ON_ERROR_STOP=1 <<'SQL'
CREATE DATABASE accounts_db_p1;
CREATE DATABASE accounts_db_p2;
SQL

psql --username "$POSTGRES_USER" --dbname accounts_db_p1 --set ON_ERROR_STOP=1 \
    --file /platform/accounts/schema.sql \
    --file /platform/accounts/seed.sql \
    --file /platform/accounts/procedures.sql
