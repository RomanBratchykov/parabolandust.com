#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repository_root"
: "${NEWSLETTERS_TEST_CONNECTION:?Set NEWSLETTERS_TEST_CONNECTION to a disposable test database.}"
command -v curl >/dev/null || { echo 'API checks require curl.' >&2; exit 1; }
export NEWSLETTERS_TEST_URL="${NEWSLETTERS_CHECK_URL:-http://127.0.0.1:5187}"
api_log="$(mktemp "${TMPDIR:-/tmp}/newsletters-checks.XXXXXX")"
export NEWSLETTERS_TEST_LOG="$api_log"
build_platform=linux
case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
        build_platform=windows
        export NEWSLETTERS_TEST_LOG="$(cygpath -m "$api_log")"
        ;;
esac
api_pid=""

cleanup() {
    if [[ -n "$api_pid" ]]; then
        kill "$api_pid" 2>/dev/null || true
        wait "$api_pid" 2>/dev/null || true
    fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

echo "API and logging checks: $NEWSLETTERS_TEST_URL"
echo "Captured API log: $NEWSLETTERS_TEST_LOG"
if curl --silent --max-time 1 "$NEWSLETTERS_TEST_URL" >/dev/null 2>&1; then
    echo "The check URL is already in use. Set NEWSLETTERS_CHECK_URL to a free localhost URL." >&2
    exit 1
fi

dotnet build Services/Newsletters/Newsletters.Api/Newsletters.Api.csproj -m:1
# Run the built process directly so cleanup owns the API process, not a launcher.
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__NewslettersDb="$NEWSLETTERS_TEST_CONNECTION" \
dotnet "Services/Newsletters/Newsletters.Api/bin/$build_platform/Debug/net10.0/Newsletters.Api.dll" \
    --urls "$NEWSLETTERS_TEST_URL" >"$api_log" 2>&1 &
api_pid=$!

ready=false
for _ in {1..30}; do
    if ! kill -0 "$api_pid" 2>/dev/null; then
        echo "The check API exited during startup." >&2
        cat "$api_log" >&2
        exit 1
    fi
    if curl --fail --silent --max-time 1 "$NEWSLETTERS_TEST_URL/swagger/v1/swagger.json" >/dev/null; then
        ready=true
        break
    fi
    sleep 1
done
if [[ "$ready" != true ]]; then
    echo "The check API did not become ready within 30 attempts." >&2
    cat "$api_log" >&2
    exit 1
fi

dotnet test Services/Newsletters/Newsletters.Tests/Newsletters.Tests.csproj --no-restore -m:1 \
    --filter 'FullyQualifiedName~NewsletterApiTests|FullyQualifiedName~NewsletterLogTests'
