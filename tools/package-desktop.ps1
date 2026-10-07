param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputDir,
    [string]$PrivateConfigPath,
    [string]$Version,
    [switch]$SelfContained,
    [switch]$CreateRootLauncher,
    [switch]$LocalOnly,
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

function Sync-WebRuntime {
    param(
        [string]$SourceRoot,
        [string]$DestinationRoot
    )

    $resolvedSourceRoot = [System.IO.Path]::GetFullPath($SourceRoot)
    if (-not (Test-Path $resolvedSourceRoot)) {
        throw "Web runtime source directory not found: $resolvedSourceRoot"
    }

    $resolvedDestinationRoot = [System.IO.Path]::GetFullPath($DestinationRoot)
    $targetWebRoot = [System.IO.Path]::GetFullPath((Join-Path $resolvedDestinationRoot "web"))
    $publishRootWithSeparator = $resolvedDestinationRoot.TrimEnd('\') + '\'
    if (-not $targetWebRoot.StartsWith($publishRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe web runtime target path: $targetWebRoot"
    }

    if (Test-Path $targetWebRoot) {
        Remove-Item -LiteralPath $targetWebRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $targetWebRoot | Out-Null

    foreach ($relativePath in @("index.html", "assets", "favicon.ico", "manifest.webmanifest", "robots.txt", "shell-static")) {
        $sourcePath = Join-Path $resolvedSourceRoot $relativePath
        if (-not (Test-Path $sourcePath)) {
            continue
        }

        Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $targetWebRoot $relativePath) -Recurse -Force
    }
}

function Write-RootLauncher {
    param([string]$DestinationRoot)

    $resolvedDestinationRoot = [System.IO.Path]::GetFullPath($DestinationRoot)
    $releaseRoot = Split-Path -Parent $resolvedDestinationRoot
    $folderName = Split-Path -Leaf $resolvedDestinationRoot
    $desktopExecutable = Join-Path $resolvedDestinationRoot "Xke.DesktopHost.exe"
    if (-not (Test-Path -LiteralPath $desktopExecutable -PathType Leaf)) {
        throw "Desktop executable not found: $desktopExecutable"
    }

    $launcherPath = Join-Path $releaseRoot "$folderName.exe"
    $escapedFolderName = $folderName.Replace("\", "\\").Replace('"', '\"')
    $source = @"
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var folderName = "$escapedFolderName";
        var targetPath = Path.Combine(baseDirectory, folderName, "Xke.DesktopHost.exe");
        if (!File.Exists(targetPath))
        {
            MessageBox.Show(
                "未找到程序文件夹。请保持启动器和 " + folderName + " 文件夹在同一目录。",
                folderName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = targetPath,
            WorkingDirectory = Path.GetDirectoryName(targetPath),
            UseShellExecute = true
        });
        return 0;
    }
}
"@

    if (Test-Path -LiteralPath $launcherPath) {
        Remove-Item -LiteralPath $launcherPath -Force
    }

    $frameworkCsc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path -LiteralPath $frameworkCsc -PathType Leaf)) {
        $frameworkCsc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
    }

    if (Test-Path -LiteralPath $frameworkCsc -PathType Leaf) {
        $sourcePath = Join-Path ([System.IO.Path]::GetTempPath()) "xke-launcher-$([Guid]::NewGuid().ToString('N')).cs"
        try {
            Set-Content -LiteralPath $sourcePath -Value $source -Encoding UTF8
            $compilerArgs = @(
                "/nologo",
                "/target:winexe",
                "/platform:anycpu",
                "/out:$launcherPath",
                "/reference:System.dll",
                "/reference:System.Windows.Forms.dll"
            )
            $iconPath = Join-Path $repoRoot "src\Xke.DesktopHost\Assets\app.ico"
            if (Test-Path -LiteralPath $iconPath -PathType Leaf) {
                $compilerArgs += "/win32icon:$iconPath"
            }

            $compilerArgs += $sourcePath
            & $frameworkCsc @compilerArgs
            if ($LASTEXITCODE -ne 0) {
                throw "Root launcher compilation failed."
            }
        }
        finally {
            if (Test-Path -LiteralPath $sourcePath) {
                Remove-Item -LiteralPath $sourcePath -Force
            }
        }
    } else {
        Add-Type `
            -TypeDefinition $source `
            -ReferencedAssemblies @("System.dll", "System.Windows.Forms.dll") `
            -OutputAssembly $launcherPath `
            -OutputType WindowsApplication
    }

    Write-Host "Root launcher: $launcherPath"
}

$publishDir = Resolve-FullPath $OutputDir
$projectPath = Join-Path $repoRoot "src\Xke.DesktopHost\Xke.DesktopHost.csproj"
$updaterProjectPath = Join-Path $repoRoot "src\Xke.Updater\Xke.Updater.csproj"
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$versionArguments = @()
if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $versionArguments = @("/p:Version=$($Version.Trim())")
}

$env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet_cli"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "Publishing Xke.DesktopHost..."
& dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained $selfContainedValue `
    -o $publishDir `
    @versionArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

Write-Host "Publishing Xke.Updater..."
& dotnet publish $updaterProjectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained $selfContainedValue `
    -o $publishDir `
    @versionArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish Xke.Updater failed."
}

$updaterExe = Join-Path $publishDir "Xke.Updater.exe"
$updaterDll = Join-Path $publishDir "Xke.Updater.dll"
if (-not (Test-Path $updaterExe) -and -not (Test-Path $updaterDll)) {
    throw "Published package is missing Xke.Updater."
}

Write-Host "Syncing web runtime..."
Sync-WebRuntime -SourceRoot (Join-Path $repoRoot "web") -DestinationRoot $publishDir
if ($CreateRootLauncher) {
    Write-RootLauncher -DestinationRoot $publishDir
}

if ($LocalOnly) {
    $localOnlyConfig = [ordered]@{
        schema_version = 1
        local_only = $true
    } | ConvertTo-Json
    Set-Content -LiteralPath (Join-Path $publishDir "xke.runtime.json") -Value $localOnlyConfig -Encoding UTF8
    Write-Host "Local-only runtime config written."
}
elseif (-not $NoInject) {
    $injectArgs = @{
        PublishDir = $publishDir
    }
    if (-not [string]::IsNullOrWhiteSpace($PrivateConfigPath)) {
        $injectArgs.PrivateConfigPath = $PrivateConfigPath
    }

    & (Join-Path $PSScriptRoot "inject-private-cloud-config.ps1") @injectArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Private cloud config injection failed."
    }
}

Write-Host "Package output: $publishDir"
