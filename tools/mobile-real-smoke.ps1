param(
    [string]$User = "",
    [string]$DataRoot = "",
    [int]$Port = 0,
    [string]$PasswordEnvVar = "",
    [string]$ProjectPath = "",
    [string]$PreloginMode = "",
    [switch]$NoRestart,
    [switch]$Cleanup
)

$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "mobile-real-smoke.mjs"
if (-not (Test-Path $scriptPath)) {
    throw "mobile-real-smoke.mjs not found: $scriptPath"
}

if ($ProjectPath) {
    Write-Warning "-ProjectPath is ignored; the Node smoke resolves the WebHost project from the repository root."
}
if ($PreloginMode) {
    Write-Warning "-PreloginMode is ignored; the mobile real smoke uses the unified auto login path."
}
if ($NoRestart) {
    Write-Warning "-NoRestart is ignored; restart validation is mandatory for local credential verification."
}
if ($Cleanup) {
    Write-Warning "-Cleanup is ignored; the Node smoke keeps the temporary data root for diagnostics."
}

if ($PasswordEnvVar) {
    $password = [Environment]::GetEnvironmentVariable($PasswordEnvVar, "Process")
    if (-not $password) {
        $password = [Environment]::GetEnvironmentVariable($PasswordEnvVar, "User")
    }
    if (-not $password) {
        $password = [Environment]::GetEnvironmentVariable($PasswordEnvVar, "Machine")
    }
    if (-not $password) {
        throw "Password environment variable not found: $PasswordEnvVar"
    }

    # Explicitly named unsafe bridge for compatibility with old PowerShell callers.
    # Prefer interactive hidden input when running the Node smoke directly.
    $env:XKE_REAL_PASSWORD_UNSAFE = $password
}

$node = "node"
$nodeExe = "C:\Program Files\nodejs\node.exe"
if (Test-Path $nodeExe) {
    $node = $nodeExe
}

$args = @($scriptPath)
if ($User) {
    $args += "--user=$User"
}
if ($DataRoot) {
    $args += "--data-dir=$DataRoot"
}
if ($Port -gt 0) {
    $args += "--port=$Port"
}

& $node @args
exit $LASTEXITCODE
