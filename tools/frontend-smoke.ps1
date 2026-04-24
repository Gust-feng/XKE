param(
    [switch]$SkipSyntaxCheck
)

$ErrorActionPreference = "Stop"

$dotnetRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $dotnetRoot
$webRoot = Join-Path $repoRoot "web"

function Resolve-ToolPath {
    param(
        [string[]]$Names,
        [string]$FallbackPath
    )

    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) {
            return $command.Source
        }
    }

    if ($FallbackPath -and (Test-Path $FallbackPath)) {
        return $FallbackPath
    }

    return ""
}

function Invoke-Checked {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $FilePath $($Arguments -join ' ')"
    }
}

if (-not (Test-Path $webRoot)) {
    throw "Web root not found: $webRoot"
}

$node = Resolve-ToolPath -Names @("node.exe", "node") -FallbackPath "C:\Program Files\nodejs\node.exe"
$npm = Resolve-ToolPath -Names @("npm.cmd", "npm") -FallbackPath "C:\Program Files\nodejs\npm.cmd"

if ([string]::IsNullOrWhiteSpace($node)) {
    throw "Node.js was not found. Expected it on PATH or at C:\Program Files\nodejs\node.exe"
}

Push-Location $webRoot
$previousPath = $env:Path
try {
    $nodeDir = Split-Path -Parent $node
    if (-not [string]::IsNullOrWhiteSpace($nodeDir)) {
        $env:Path = "$nodeDir;$env:Path"
    }

    Write-Host "Node: $(& $node --version)"
    if (-not [string]::IsNullOrWhiteSpace($npm)) {
        Write-Host "npm: $(& $npm --version)"
    }

    if (-not $SkipSyntaxCheck) {
        Write-Host "Checking frontend JavaScript syntax..."
        $jsFiles = Get-ChildItem -Path (Join-Path $webRoot "assets") -Recurse -Filter "*.js" |
            Sort-Object FullName
        foreach ($file in $jsFiles) {
            $relativePath = Resolve-Path -Path $file.FullName -Relative
            Invoke-Checked -FilePath $node -Arguments @("--check", $relativePath)
        }
    }

    Write-Host "Running frontend tests..."
    if (-not [string]::IsNullOrWhiteSpace($npm)) {
        Invoke-Checked -FilePath $npm -Arguments @("test")
    }
    else {
        Invoke-Checked -FilePath $node -Arguments @("--test")
    }
}
finally {
    $env:Path = $previousPath
    Pop-Location
}
