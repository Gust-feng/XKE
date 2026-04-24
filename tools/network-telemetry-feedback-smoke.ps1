param(
    [switch]$Detailed
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    $args = @(
        "test",
        ".\\tests\\Xke.WebHost.Tests\\Xke.WebHost.Tests.csproj",
        "--filter",
        "FullyQualifiedName~NetworkTelemetryFeedbackWebHostTests"
    )

    if ($Detailed) {
        $args += @("--logger", "console;verbosity=detailed")
    }

    dotnet @args
}
finally {
    Pop-Location
}
