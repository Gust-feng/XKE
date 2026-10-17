#Requires -Version 7.0
# Synthetic API integration only: no WPF window, real school, registry writes or user data.
[CmdletBinding()]
param([string]$WebRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $WebRoot) { $WebRoot = Join-Path $repoRoot 'frontend/dist' }
. (Join-Path $PSScriptRoot 'package-release-validation.ps1')
$null = Assert-FrontendRuntime ([IO.Path]::GetFullPath($WebRoot))
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet_cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$project = Join-Path $repoRoot 'tests/Xke.FrontendIntegrationHost/Xke.FrontendIntegrationHost.csproj'
& dotnet build $project --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Synthetic integration host build failed.' }
$dll = Join-Path $repoRoot 'tests/Xke.FrontendIntegrationHost/bin/Debug/net8.0/Xke.FrontendIntegrationHost.dll'
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('xke-integration-check-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$out = Join-Path $scratch 'stdout.log'
$err = Join-Path $scratch 'stderr.log'
$process = $null
$url = $null
$previousUrl = $env:XKE_INTEGRATION_URL
try {
    $process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @("`"$dll`"", "`"$WebRoot`"") `
        -WindowStyle Hidden -WorkingDirectory $repoRoot -RedirectStandardOutput $out -RedirectStandardError $err -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ([DateTime]::UtcNow -lt $deadline) {
        $log = Get-Content -LiteralPath $out -Raw -ErrorAction SilentlyContinue
        if ($log -match '(?m)^INTEGRATION_URL=(http://127\.0\.0\.1:[0-9]+)\s*$') { $url = $Matches[1]; break }
        if ($process.HasExited) { throw 'Synthetic integration host exited before becoming ready.' }
        Start-Sleep -Milliseconds 200
    }
    if (-not $url) { throw 'Synthetic integration host did not become ready.' }
    $env:XKE_INTEGRATION_URL = $url
    Push-Location $repoRoot
    try {
        & npm --prefix frontend run test:integration
        if ($LASTEXITCODE -ne 0) { throw 'Synthetic HTTP integration failed.' }
    } finally { Pop-Location }
} finally {
    $env:XKE_INTEGRATION_URL = $previousUrl
    if ($url) {
        try { Invoke-RestMethod -Method Post -Uri "$url/__integration/stop" -TimeoutSec 5 | Out-Null } catch { Write-Warning 'Graceful fixture-host shutdown failed.' }
    }
    if ($process -and -not $process.WaitForExit(10000)) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    Write-Host "Synthetic integration logs: $scratch"
}
