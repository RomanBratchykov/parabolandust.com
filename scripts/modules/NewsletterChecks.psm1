function Invoke-NewsletterChecks {
    param(
        [Parameter(Mandatory = $true)] [string] $ConnectionString,
        [string] $Url = 'http://127.0.0.1:5187'
    )

    $repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $logPath = [System.IO.Path]::GetTempFileName()
    $errorLogPath = [System.IO.Path]::GetTempFileName()
    $apiProcess = $null
    $variableNames = @('NEWSLETTERS_TEST_URL', 'NEWSLETTERS_TEST_LOG',
        'ASPNETCORE_ENVIRONMENT', 'ConnectionStrings__NewslettersDb')
    $previousValues = @{}
    foreach ($name in $variableNames) {
        $previousValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    Push-Location $repositoryRoot
    try {
        Write-Host "API and logging checks: $Url"
        Write-Host "Captured API log: $logPath (stderr: $errorLogPath)"
        $occupied = $false
        try {
            $null = Invoke-WebRequest -Uri "$Url/swagger/v1/swagger.json" -TimeoutSec 1
            $occupied = $true
        } catch {
            $occupied = $null -ne $_.Exception.Response
        }
        if ($occupied) { throw 'The check URL is already in use. Set NEWSLETTERS_CHECK_URL to a free localhost URL.' }

        & dotnet build Services/Newsletters/Newsletters.Api/Newsletters.Api.csproj -m:1
        if ($LASTEXITCODE -ne 0) { throw 'The check API build failed.' }
        $env:NEWSLETTERS_TEST_URL = $Url
        $env:NEWSLETTERS_TEST_LOG = $logPath
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:ConnectionStrings__NewslettersDb = $ConnectionString
        $buildPlatform = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
                [System.Runtime.InteropServices.OSPlatform]::Windows)) { 'windows' } else { 'linux' }
        $apiDll = "Services/Newsletters/Newsletters.Api/bin/$buildPlatform/Debug/net10.0/Newsletters.Api.dll"
        $apiProcess = Start-Process dotnet -ArgumentList @($apiDll, '--urls', $Url) -PassThru -NoNewWindow `
            -RedirectStandardOutput $logPath -RedirectStandardError $errorLogPath

        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            if ($apiProcess.HasExited) { throw 'The check API exited during startup.' }
            try {
                $response = Invoke-WebRequest -Uri "$Url/swagger/v1/swagger.json" -TimeoutSec 1
                if ($response.StatusCode -eq 200) { $ready = $true; break }
            } catch { }
            Start-Sleep -Seconds 1
        }
        if (-not $ready) { throw 'The check API did not become ready within 30 attempts.' }

        & dotnet test Services/Newsletters/Newsletters.Tests/Newsletters.Tests.csproj --no-restore -m:1 `
            --filter 'FullyQualifiedName~NewsletterApiTests|FullyQualifiedName~NewsletterLogTests'
        if ($LASTEXITCODE -ne 0) { throw "API/logging checks failed. Inspect $logPath and $errorLogPath." }
    }
    finally {
        if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
            Stop-Process -Id $apiProcess.Id
            $apiProcess.WaitForExit()
        }
        foreach ($name in $variableNames) {
            [Environment]::SetEnvironmentVariable($name, $previousValues[$name], 'Process')
        }
        Pop-Location
    }
}

Export-ModuleMember -Function Invoke-NewsletterChecks
