$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repositoryRoot "db/docker-compose.yml"
$postgresContainer = "pg-test"
$testDatabase = "newsletters_lab3_test"
$applicationDatabase = "newsletters_lab3"
$postgresPassword = "secret"
$platform = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    'Windows'
} elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Linux)) {
    'Linux'
} else {
    throw 'This script supports Windows and Linux.'
}
$testConnection = "Host=localhost;Port=5432;Database=$testDatabase;Username=postgres;Password=$postgresPassword"
$applicationConnection = "Host=localhost;Port=5432;Database=$applicationDatabase;Username=postgres;Password=$postgresPassword"

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)] [string] $Command,
        [Parameter(Mandatory = $true)] [string[]] $Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}

function Ensure-Database {
    param([Parameter(Mandatory = $true)] [string] $DatabaseName)

    $exists = & docker exec $postgresContainer psql -U postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$DatabaseName'"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not check whether database '$DatabaseName' exists."
    }

    if ($exists.Trim() -ne "1") {
        Invoke-CheckedCommand "docker" @("exec", $postgresContainer, "createdb", "-U", "postgres", $DatabaseName)
    }
}

Push-Location $repositoryRoot
$previousPath = $env:PATH
$previousTestConnection = $env:NEWSLETTERS_TEST_CONNECTION
$previousApplicationConnection = $env:ConnectionStrings__NewslettersDb
try {
    Invoke-CheckedCommand "docker" @("compose", "-f", $composeFile, "up", "-d", "db")

    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        & docker exec $postgresContainer pg_isready -U postgres *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) {
        throw "PostgreSQL did not become ready in the '$postgresContainer' container."
    }

    Ensure-Database $testDatabase
    Ensure-Database $applicationDatabase

    $env:NEWSLETTERS_TEST_CONNECTION = $testConnection
    Invoke-CheckedCommand "dotnet" @("test", "Services/Newsletters/Newsletters.Tests/Newsletters.Tests.csproj")

    $env:ConnectionStrings__NewslettersDb = $applicationConnection
    Invoke-CheckedCommand "dotnet" @("run", "--project", "Services/Newsletters/Newsletters.Api/Newsletters.Api.csproj")
}
finally {
    $env:PATH = $previousPath
    $env:NEWSLETTERS_TEST_CONNECTION = $previousTestConnection
    $env:ConnectionStrings__NewslettersDb = $previousApplicationConnection
    Pop-Location
}
