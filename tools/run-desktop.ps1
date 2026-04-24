param(
    [string]$Configuration = "Debug",
    [string]$DataRoot = "",
    [string]$WebRoot = "",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Set-InheritedEnvVar {
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        Remove-Item "Env:$Name" -ErrorAction SilentlyContinue
        return
    }

    Set-Item "Env:$Name" $Value
}

Push-Location $repoRoot
try {
    $previousDataRoot = $env:XKE_DOTNET_DATA_DIR
    $previousWebRoot = $env:XKE_WEB_ROOT

    try {
        if (-not [string]::IsNullOrWhiteSpace($DataRoot)) {
            Set-InheritedEnvVar -Name "XKE_DOTNET_DATA_DIR" -Value $DataRoot
        }
        if (-not [string]::IsNullOrWhiteSpace($WebRoot)) {
            Set-InheritedEnvVar -Name "XKE_WEB_ROOT" -Value $WebRoot
        }

        if (-not $NoBuild) {
            dotnet build .\src\Xke.DesktopHost\Xke.DesktopHost.csproj -c $Configuration
        }

        $args = @(
            "run",
            "--project",
            ".\src\Xke.DesktopHost\Xke.DesktopHost.csproj",
            "-c",
            $Configuration,
            "--no-launch-profile"
        )
        if ($NoBuild) {
            $args += "--no-build"
        }

        Write-Host "Starting DesktopHost ($Configuration)"
        if (-not [string]::IsNullOrWhiteSpace($DataRoot)) {
            Write-Host "Data root: $DataRoot"
        }
        if (-not [string]::IsNullOrWhiteSpace($WebRoot)) {
            Write-Host "Web root: $WebRoot"
        }

        dotnet @args
    }
    finally {
        Set-InheritedEnvVar -Name "XKE_DOTNET_DATA_DIR" -Value $previousDataRoot
        Set-InheritedEnvVar -Name "XKE_WEB_ROOT" -Value $previousWebRoot
    }
}
finally {
    Pop-Location
}
