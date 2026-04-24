param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    dotnet test .\XKE.DotNet8.sln -c $Configuration
}
finally {
    Pop-Location
}
