#Requires -Version 7.0
<#
.SYNOPSIS
Build the single formal Windows release using repository-local Velopack 1.2.161.
.DESCRIPTION
Requires a runtime frontend build. Defaults to Directory.Build.props Version and
self-contained .NET 8.0.32. No credentials/private schedule configuration are copied.
Produces Setup, Portable, full update package and Velopack feeds in a new/empty output.
Does not install, launch, upload or modify the user's runtime data/registry.
.EXAMPLE
pwsh -File tools/package-desktop.ps1
.EXAMPLE
pwsh -File tools/package-desktop.ps1 -Version 1.0.0 -OutputDir artifacts/releases/1.0.0
#>
[CmdletBinding()]
param(
    [ValidateSet('Release')][string]$Configuration = 'Release',
    [ValidateSet('win-x64')][string]$RuntimeIdentifier = 'win-x64',
    [string]$OutputDir,
    [string]$WebRoot,
    [string]$Version,
    [ValidateSet('win')][string]$Channel = 'win',
    [string]$CampusSelfServiceConfigPath,
    # Deprecated arguments are rejected, rather than silently copying private material or using an old launcher.
    [string]$ScheduleApiConfigPath,
    [switch]$SelfContained = $true,
    [switch]$CreateRootLauncher
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'package-release-validation.ps1')
$repoRoot = Split-Path -Parent $PSScriptRoot
$runtimeVersion = '8.0.32'
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.txt') -PathType Leaf)) { throw 'Third-party notices are required before publishing.' }
if ($ScheduleApiConfigPath) { throw 'Formal releases never accept ScheduleApiConfigPath or API tokens. Configure private services locally after installation.' }
if (-not $SelfContained) { throw 'Formal desktop releases must be self-contained.' }
if ($CreateRootLauncher) { throw 'Velopack generates the official launcher; CreateRootLauncher is retired.' }
if (@(Get-ChildItem Env: | Where-Object Name -Like 'VPK_*').Count -gt 0) { throw 'Formal packaging does not accept VPK_* environment overrides.' }
$Version = Get-DesktopReleaseVersion $repoRoot $Version
if (-not $OutputDir) { $OutputDir = "artifacts/releases/$Version" }
$finalRoot = Resolve-ReleasePath $OutputDir $repoRoot
Assert-EmptyReleaseOutput $finalRoot
$frontendRoot = if ($WebRoot) { Resolve-ReleasePath $WebRoot $repoRoot } else { Join-Path $repoRoot 'frontend/dist' }
$null = Assert-FrontendRuntime $frontendRoot
$campusConfig = $null
if ($CampusSelfServiceConfigPath) {
    $campusConfig = Read-CampusReleaseConfig (Resolve-ReleasePath $CampusSelfServiceConfigPath $repoRoot)
    Write-Host 'Campus account summary: endpoint configuration included (no credentials).'
} else {
    Write-Warning 'Campus account summary is not configured: use -CampusSelfServiceConfigPath for a release that includes account information.'
}
$manifestPath = Join-Path $repoRoot '.config/dotnet-tools.json'
$manifest = Read-ReleaseJsonObject $manifestPath
if ($manifest.tools.vpk.version -cne '1.2.161' -or $manifest.tools.vpk.commands -cnotcontains 'vpk' -or -not $manifest.isRoot) {
    throw 'Repository local tool manifest must pin vpk 1.2.161.'
}
Assert-ReleaseRuntimeAvailable $RuntimeIdentifier $runtimeVersion

$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet_cli'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $repoRoot
$stageRoot = $null
try {
    & dotnet tool restore --tool-manifest $manifestPath
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore pinned local vpk 1.2.161.' }
    # Stage next to the final directory so the final rename is same-volume and atomic.
    $parent = Split-Path -Parent $finalRoot
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $stageRoot = Join-Path $parent ('.xke-package-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    $publishRoot = Join-Path $stageRoot 'publish'
    $releaseRoot = Join-Path $stageRoot 'releases'
    $project = Join-Path $repoRoot 'src/Xke.DesktopHost/Xke.DesktopHost.csproj'
    & dotnet msbuild $project -t:PrepareCaptchaAssets -nologo
    if ($LASTEXITCODE -ne 0) { throw 'Could not prepare verified app-local OCR assets.' }
    & dotnet publish $project -c $Configuration -r $RuntimeIdentifier --self-contained true `
        --artifacts-path (Join-Path $stageRoot 'build') -o $publishRoot `
        "-p:Version=$Version" "-p:RuntimeFrameworkVersion=$runtimeVersion" -p:TargetLatestRuntimePatch=true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugSymbols=false -p:DebugType=None
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed; no release output was installed.' }
    # Referenced WebHost is hosted in-process; do not ship a second launchable server or runtime diagnostics.
    foreach ($name in @('createdump.exe', 'Xke.WebHost.exe', 'Xke.WebHost.deps.json', 'Xke.WebHost.runtimeconfig.json', 'onnxruntime.lib', 'onnxruntime_providers_shared.lib')) {
        $path = Join-Path $publishRoot $name
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path }
    }
    $webTarget = Join-Path $publishRoot 'web'
    if (Test-Path -LiteralPath $webTarget) { throw 'dotnet publish unexpectedly included web content; only the explicit runtime source is permitted.' }
    New-Item -ItemType Directory -Path $webTarget | Out-Null
    foreach ($file in Assert-FrontendRuntime $frontendRoot) {
        $relative = [IO.Path]::GetRelativePath($frontendRoot, $file.FullName)
        $target = Join-Path $webTarget $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    if ($null -ne $campusConfig) {
        $campusConfig | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishRoot 'xke.campus-self-service.json') -Encoding utf8
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $publishRoot 'THIRD-PARTY-NOTICES.txt')
    Assert-DesktopPublish $publishRoot $RuntimeIdentifier $runtimeVersion ($null -ne $campusConfig)
    # Official bootstrap installs/checks WebView2. Do NOT request .NET bootstrap: the runtime is bundled.
    # No --skipVeloAppCheck, custom updater, root-launcher compilation or GitHub upload is allowed here.
    & dotnet tool run vpk -- --skip-updates pack --packId XKE --packVersion $Version --packTitle XKE `
        --packAuthors Gust-feng --packDir $publishRoot --mainExe Xke.DesktopHost.exe `
        --runtime $RuntimeIdentifier --channel $Channel --framework webview2 `
        --icon (Join-Path $repoRoot 'src/Xke.DesktopHost/Assets/app.ico') --outputDir $releaseRoot
    if ($LASTEXITCODE -ne 0) { throw 'Velopack packaging failed; no partial final release was published.' }
    Assert-VelopackRelease $releaseRoot $Version $Channel ($null -ne $campusConfig)
    Assert-EmptyReleaseOutput $finalRoot
    if (Test-Path -LiteralPath $finalRoot) { [IO.Directory]::Delete($finalRoot, $false) }
    Move-Item -LiteralPath $releaseRoot -Destination $finalRoot -ErrorAction Stop
    Write-Host "Verified formal release $Version (channel $Channel): $finalRoot"
    Get-ChildItem -LiteralPath $finalRoot -File | ForEach-Object { Write-Host "  $($_.Name)" }
    # Delete only this command's generated staging directory, never a computed user output/data root.
    $resolvedStage = [IO.Path]::GetFullPath($stageRoot)
    if ([IO.Path]::GetDirectoryName($resolvedStage) -ne $parent -or
        [IO.Path]::GetFileName($resolvedStage) -cnotmatch '^\.xke-package-[0-9a-f]{32}$') { throw 'Unsafe staging cleanup path.' }
    Assert-NoReleaseLinks $resolvedStage
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
} catch {
    if ($stageRoot) { Write-Warning "Packaging stopped. Isolated staging retained for inspection: $stageRoot" }
    throw
} finally {
    Pop-Location
}
