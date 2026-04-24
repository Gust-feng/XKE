param(
    [switch]$SkipBuild,
    [switch]$Detailed
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    if (-not $SkipBuild) {
        dotnet build .\XKE.DotNet8.sln
    }

    $args = @(
        "test",
        ".\XKE.DotNet8.sln",
        "--filter",
        "FullyQualifiedName~Desktop"
    )

    if ($Detailed) {
        $args += @("--logger", "console;verbosity=detailed")
    }

    dotnet @args
}
finally {
    Pop-Location
}
