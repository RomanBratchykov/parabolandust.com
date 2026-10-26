param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string] $Name
)

$ErrorActionPreference = 'Stop'

$platform = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    'Windows'
} elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Linux)) {
    'Linux'
} else {
    throw 'This script supports Windows and Linux.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$serviceRoot = Join-Path $repositoryRoot "Services/$Name"
$layers = @('Domain', 'Dal', 'Bll', 'Api')

if (Test-Path -LiteralPath $serviceRoot) {
    throw "Service directory already exists: $serviceRoot"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK (dotnet) must be installed and available on PATH.'
}

Write-Host "Detected platform: $platform"
New-Item -ItemType Directory -Path $serviceRoot | Out-Null
Push-Location $repositoryRoot

function Invoke-Dotnet {
    param([Parameter(Mandatory = $true)] [string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

try {
    foreach ($layer in $layers) {
        $projectName = "$Name.$layer"
        $template = if ($layer -eq 'Api') { 'webapi' } else { 'classlib' }
        $projectDir = Join-Path $serviceRoot $projectName
        Invoke-Dotnet @('new', $template, '--name', $projectName, '--output', $projectDir, '--framework', 'net10.0', '--no-restore')
    }

    $domain = "Services/$Name/$Name.Domain/$Name.Domain.csproj"
    $dal = "Services/$Name/$Name.Dal/$Name.Dal.csproj"
    $bll = "Services/$Name/$Name.Bll/$Name.Bll.csproj"
    $api = "Services/$Name/$Name.Api/$Name.Api.csproj"
    Invoke-Dotnet @('add', $dal, 'reference', $domain)
    Invoke-Dotnet @('add', $bll, 'reference', $domain, $dal)
    Invoke-Dotnet @('add', $api, 'reference', $bll)
    Invoke-Dotnet @('sln', 'Platform.slnx', 'add', $domain, $dal, $bll, $api)

    Write-Host "Created Services/$Name with Domain, Dal, Bll, and Api projects."
} catch {
    Remove-Item -LiteralPath $serviceRoot -Recurse -Force -ErrorAction SilentlyContinue
    throw
} finally {
    Pop-Location
}
