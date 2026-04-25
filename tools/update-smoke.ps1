param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet_cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$testProject = Join-Path $repoRoot "tests\Xke.WebHost.Tests\Xke.WebHost.Tests.csproj"
$arguments = @(
    "test",
    $testProject,
    "--filter",
    "FullyQualifiedName~UpdateWebHostTests"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

Write-Host "Running one-click update smoke tests..."
& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Update smoke tests failed."
}
