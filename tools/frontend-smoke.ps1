param(
    [switch]$SkipInstall,
    [switch]$SkipBuild,
    [switch]$SkipSyntaxCheck
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$frontendRoot = Join-Path $repoRoot "frontend"

function Resolve-ToolPath {
    param([string[]]$Names, [string]$FallbackPath)
    foreach ($name in $Names) {
        $command = Get-Command $name -ErrorAction SilentlyContinue
        if ($command) { return $command.Source }
    }
    if ($FallbackPath -and (Test-Path -LiteralPath $FallbackPath)) { return $FallbackPath }
    throw "Required tool not found: $($Names -join ', ')"
}

function Invoke-Checked {
    param([string]$FilePath, [string[]]$Arguments)
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $FilePath $($Arguments -join ' ')"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $frontendRoot "package.json"))) {
    throw "TypeScript frontend not found: $frontendRoot"
}
$node = Resolve-ToolPath -Names @("node.exe", "node") -FallbackPath "C:\Program Files\nodejs\node.exe"
$npm = Resolve-ToolPath -Names @("npm.cmd", "npm") -FallbackPath "C:\Program Files\nodejs\npm.cmd"
$previousPath = $env:Path
Push-Location $frontendRoot
try {
    $env:Path = "$(Split-Path -Parent $node);$env:Path"
    Write-Host "Node: $(& $node --version)"
    Write-Host "npm: $(& $npm --version)"
    if ($SkipSyntaxCheck) {
        Write-Warning "TypeScript checks run through npm run check; use -SkipBuild to skip the build."
    }
    if (-not $SkipInstall) {
        Invoke-Checked -FilePath $npm -Arguments @("ci")
    }
    elseif (-not (Test-Path -LiteralPath (Join-Path $frontendRoot "node_modules"))) {
        throw "Dependencies are missing. Run without -SkipInstall, or run npm ci in frontend/."
    }
    Write-Host "Checking the first-version React/TypeScript runtime..."
    Invoke-Checked -FilePath $npm -Arguments @("run", "check")
    Invoke-Checked -FilePath $npm -Arguments @("run", "lint")
    Invoke-Checked -FilePath $npm -Arguments @("run", "test")
    if (-not $SkipBuild) {
        Write-Host "Building the frontend (runtime by default; preview must be explicit)..."
        Invoke-Checked -FilePath $npm -Arguments @("run", "build")
    }
}
finally {
    $env:Path = $previousPath
    Pop-Location
}
