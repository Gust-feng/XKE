param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDir,
    [string]$PrivateConfigPath,
    [switch]$SelfContained,
    [switch]$NoInject
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts\publish\Xke.DesktopHost"
}

function Resolve-FullPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$publishDir = Resolve-FullPath $OutputDir
$projectPath = Join-Path $repoRoot "src\Xke.DesktopHost\Xke.DesktopHost.csproj"
$updaterProjectPath = Join-Path $repoRoot "src\Xke.Updater\Xke.Updater.csproj"
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet_cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing Xke.DesktopHost..."
& dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained $selfContainedValue `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Write-Host "Publishing Xke.Updater..."
& dotnet publish $updaterProjectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained $selfContainedValue `
    -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish Xke.Updater failed."
}

$updaterExe = Join-Path $publishDir "Xke.Updater.exe"
$updaterDll = Join-Path $publishDir "Xke.Updater.dll"
if (-not (Test-Path $updaterExe) -and -not (Test-Path $updaterDll)) {
    throw "Published package is missing Xke.Updater."
}

if (-not $NoInject) {
    $injectArgs = @(
        "-PublishDir", $publishDir
    )
    if (-not [string]::IsNullOrWhiteSpace($PrivateConfigPath)) {
        $injectArgs += @("-PrivateConfigPath", $PrivateConfigPath)
    }

    & (Join-Path $PSScriptRoot "inject-private-cloud-config.ps1") @injectArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Private cloud config injection failed."
    }
}

Write-Host "Package output: $publishDir"
