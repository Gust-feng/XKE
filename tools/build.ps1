param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    dotnet restore .\XKE.DotNet8.sln
    dotnet build .\XKE.DotNet8.sln -c $Configuration --no-restore
}
finally {
    Pop-Location
}
