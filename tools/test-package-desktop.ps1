#Requires -Version 7.0
# Focused synthetic release-gate tests. No dotnet publish, vpk pack, real configuration,
# application launch, registry access or GitHub upload takes place.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'package-release-validation.ps1')
$script:passed = 0
$root = Join-Path ([IO.Path]::GetTempPath()) ('xke-release-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null

function Test-Case {
    param([string]$Name, [scriptblock]$Action)
    & $Action
    $script:passed++
    Write-Host "PASS $Name"
}
function Assert-Rejected {
    param([scriptblock]$Action, [string]$Message = '.')
    try { & $Action | Out-Null } catch {
        if ($_.Exception.Message -notmatch $Message) { throw "Wrong rejection: $($_.Exception.Message)" }
        return
    }
    throw 'Expected rejection, but input was accepted.'
}
function New-FrontendFixture {
    $path = Join-Path $root ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $path 'assets') -Force | Out-Null
    '{"mode":"runtime","build":"1234567890abcdef"}' | Set-Content (Join-Path $path 'frontend-build.json')
    '<script type="module" src="./assets/index-12345678.js"></script><link rel="stylesheet" href="./assets/index-12345678.css">' | Set-Content (Join-Path $path 'index.html')
    'export const fixture=true;' | Set-Content (Join-Path $path 'assets/index-12345678.js')
    'body{}' | Set-Content (Join-Path $path 'assets/index-12345678.css')
    return $path
}
function New-PublishFixture {
    $path = Join-Path $root ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $path | Out-Null
    'synthetic' | Set-Content (Join-Path $path 'Xke.DesktopHost.exe')
    'synthetic' | Set-Content (Join-Path $path 'Xke.DesktopHost.dll')
    'synthetic' | Set-Content (Join-Path $path 'Velopack.dll')
    'synthetic public notices' | Set-Content (Join-Path $path 'THIRD-PARTY-NOTICES.txt')
    $libraries = [ordered]@{ 'Velopack/1.2.161' = @{} }
    foreach ($package in Get-RequiredRuntimePacks win-x64) { $libraries["runtimepack.$package/8.0.32"] = @{} }
    @{ libraries = $libraries } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $path 'Xke.DesktopHost.deps.json')
    $frameworks = @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App', 'Microsoft.AspNetCore.App') |
        ForEach-Object { @{ name = $_; version = '8.0.32' } }
    @{ runtimeOptions = @{ includedFrameworks = @($frameworks) } } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $path 'Xke.DesktopHost.runtimeconfig.json')
    $web = New-FrontendFixture
    Copy-Item -LiteralPath $web -Destination (Join-Path $path 'web') -Recurse
    return $path
}
function Write-SyntheticZip {
    param([string]$Path, [hashtable]$Entries)
    $zip = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($pair in $Entries.GetEnumerator()) {
            $writer = [IO.StreamWriter]::new($zip.CreateEntry($pair.Key).Open())
            try { $writer.Write([string]$pair.Value) } finally { $writer.Dispose() }
        }
    } finally { $zip.Dispose() }
}
function New-ReleaseFixture {
    param([string]$Channel = 'win', [string]$ExtraArchivePath, [switch]$IncludeCampusConfig)
    $path = Join-Path $root ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $path | Out-Null
    $names = Get-VelopackReleaseNames 1.0.0 $Channel
    $nuspec = "<package><metadata><id>XKE</id><version>1.0.0</version><channel>$Channel</channel><mainExe>Xke.DesktopHost.exe</mainExe><runtimeDependencies>webview2</runtimeDependencies></metadata></package>"
    $full = @{ 'XKE.nuspec' = $nuspec; '[Content_Types].xml' = '<Types/>'; '_rels/.rels' = '<Relationships/>' }
    $portable = @{ 'Update.exe' = 'synthetic'; 'XKE.exe' = 'synthetic'; '.portable' = '' }
    foreach ($relative in @('Xke.DesktopHost.exe', 'Xke.DesktopHost.dll', 'Velopack.dll', 'Xke.DesktopHost.deps.json', 'Xke.DesktopHost.runtimeconfig.json', 'sq.version', 'web/index.html', 'web/frontend-build.json', 'THIRD-PARTY-NOTICES.txt')) {
        $full["lib/app/$relative"] = 'synthetic'
        $portable["current/$relative"] = 'synthetic'
    }
    if ($IncludeCampusConfig) {
        $config = '{"endpoint":"https://campus.example.invalid/Self","timeout_seconds":8}'
        $full['lib/app/xke.campus-self-service.json'] = $config
        $portable['current/xke.campus-self-service.json'] = $config
    }
    $full['lib/app/Squirrel.exe'] = 'synthetic'
    $full['lib/app/XKE_ExecutionStub.exe'] = 'synthetic'
    if ($ExtraArchivePath) { $full[$ExtraArchivePath] = 'SYNTHETIC_NOT_A_SECRET' }
    Write-SyntheticZip (Join-Path $path $names.Full) $full
    Write-SyntheticZip (Join-Path $path $names.Portable) $portable
    'MZ synthetic installer fixture, never executed' | Set-Content (Join-Path $path $names.Installer)
    'synthetic legacy feed' | Set-Content (Join-Path $path $names.Legacy)
    $package = Join-Path $path $names.Full
    @{ Assets = @(@{ PackageId = 'XKE'; Version = '1.0.0'; Type = 'Full'; FileName = $names.Full; Size = (Get-Item $package).Length;
        SHA256 = (Get-FileHash $package -Algorithm SHA256).Hash; SHA1 = (Get-FileHash $package -Algorithm SHA1).Hash }) } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $path $names.Feed)
    @('Full', 'Portable', 'Installer') | ForEach-Object { @{ RelativeFileName = $names[$_]; Type = $_ } } |
        ConvertTo-Json | Set-Content (Join-Path $path $names.Assets)
    return $path
}

try {
    Test-Case 'valid runtime Vite fixture' { $null = Assert-FrontendRuntime (New-FrontendFixture) }
    foreach ($bad in @('null', '[]', '{}', '{"mode":"preview","build":"1234567890abcdef"}', '{"mode":"RUNTIME","build":"1234567890abcdef"}', '{"mode":"runtime","build":"bad"}', '{"mode":"runtime","build":17}', '{"mode":"runtime","build":"1234567890abcdef","token":"synthetic"}', '{')) {
        Test-Case "reject metadata $bad" {
            $f = New-FrontendFixture; $bad | Set-Content (Join-Path $f 'frontend-build.json')
            Assert-Rejected { Assert-FrontendRuntime $f }
        }
    }
    Test-Case 'explicit WebRoot cannot omit metadata; no final output created' {
        $f = New-FrontendFixture; Remove-Item -LiteralPath (Join-Path $f 'frontend-build.json')
        $output = Join-Path $root 'preflight-must-not-create-output'
        Assert-Rejected { & (Join-Path $PSScriptRoot 'package-desktop.ps1') -Version 1.0.0 -WebRoot $f -OutputDir $output } 'JSON'
        if (Test-Path -LiteralPath $output) { throw 'Preflight changed final output.' }
    }
    foreach ($bad in @('.env', '.env.local', 'account.json', 'anything.json', 'credential.dpapi', 'cookies.txt', 'debug.log', 'assets/token-12345678.json', 'assets/index.js', 'assets/index-12345678.js.map')) {
        Test-Case "reject frontend extra $bad" {
            $f = New-FrontendFixture; 'synthetic' | Set-Content -LiteralPath (Join-Path $f $bad)
            Assert-Rejected { Assert-FrontendRuntime $f } 'Unexpected frontend'
        }
    }
    foreach ($bad in @('./assets/missing-12345678.js', 'https://example.invalid/index.js', '//example.invalid/index.js', './assets/../index.html', './assets/index-12345678.js?token=synthetic', './assets/%2e%2e/index.js', '/assets/index-12345678.js')) {
        Test-Case "reject index reference $bad" {
            $f = New-FrontendFixture; "<script src=`"$bad`"></script>" | Set-Content (Join-Path $f 'index.html')
            Assert-Rejected { Assert-FrontendRuntime $f } 'reference'
        }
    }
    Test-Case 'reject missing script entry' {
        $f = New-FrontendFixture; '<link href="./assets/index-12345678.css">' | Set-Content (Join-Path $f 'index.html')
        Assert-Rejected { Assert-FrontendRuntime $f } 'JavaScript'
    }
    Test-Case 'resource link does not substitute for a Vite module entry' {
        $f = New-FrontendFixture; '<a href="./assets/index-12345678.js">not an entry</a>' | Set-Content (Join-Path $f 'index.html')
        Assert-Rejected { Assert-FrontendRuntime $f } 'module entry'
    }
    Test-Case 'reject unquoted index reference' {
        $f = New-FrontendFixture; '<script src=./assets/index-12345678.js></script>' | Set-Content (Join-Path $f 'index.html')
        Assert-Rejected { Assert-FrontendRuntime $f } 'quoted'
    }
    Test-Case 'reject linked frontend without following junction' {
        $f = New-FrontendFixture; $target = New-FrontendFixture
        $junction = Join-Path $f 'linked'
        New-Item -ItemType Junction -Path $junction -Target $target | Out-Null
        try { Assert-Rejected { Assert-FrontendRuntime $f } 'Linked' }
        finally { Remove-Item -LiteralPath $junction -Force }
    }
    Test-Case 'allow new or empty output only' {
        $p = Join-Path $root 'output'; Assert-EmptyReleaseOutput $p
        New-Item -ItemType Directory -Path $p | Out-Null; Assert-EmptyReleaseOutput $p
        'synthetic' | Set-Content (Join-Path $p '.hidden')
        Assert-Rejected { Assert-EmptyReleaseOutput $p } 'not empty'
    }
    Test-Case 'read formal version from main props' {
        $p = Join-Path $root 'props'; New-Item -ItemType Directory -Path $p | Out-Null
        '<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>' | Set-Content (Join-Path $p 'Directory.Build.props')
        if ((Get-DesktopReleaseVersion $p '') -cne '1.0.0') { throw 'Wrong default version.' }
    }
    foreach ($bad in @('V1.0.0', '1.0.0.0', '1.0.0-beta', '01.0.0', '1.0.0+metadata')) {
        Test-Case "reject non-formal version $bad" { Assert-Rejected { Get-DesktopReleaseVersion $root $bad } 'stable' }
    }
    $configFile = Join-Path $root 'campus.synthetic.json'
    Test-Case 'campus optional whitelist with default timeout' {
        '{"endpoint":"http://campus.example.invalid/Self"}' | Set-Content $configFile
        $c = Read-CampusReleaseConfig $configFile
        if ($c.timeout_seconds -ne 8 -or $c.Count -ne 2) { throw 'Invalid campus normalization.' }
    }
    foreach ($bad in @('{"endpoint":"https://campus.example.invalid/Self","token":"synthetic"}', '{"endpoint":"https://u:p@campus.example.invalid/Self"}', '{"endpoint":"https://campus.example.invalid/Self?token=synthetic"}', '{"endpoint":"file:///Self"}', '{"endpoint":"https://campus.example.invalid/other"}', '{"endpoint":"https://campus.example.invalid/Self","timeout_seconds":0}', '{"endpoint":"https://campus.example.invalid/Self","timeout_seconds":61}', '{"endpoint":"https://campus.example.invalid/Self","timeout_seconds":"8"}')) {
        Test-Case "reject campus config $bad" { $bad | Set-Content $configFile; Assert-Rejected { Read-CampusReleaseConfig $configFile } }
    }
    Test-Case 'private schedule injection is explicitly rejected before file read' {
        Assert-Rejected { & (Join-Path $PSScriptRoot 'package-desktop.ps1') -ScheduleApiConfigPath 'does-not-exist.synthetic' } 'never accept'
    }
    Test-Case 'no legacy root launcher' { Assert-Rejected { & (Join-Path $PSScriptRoot 'package-desktop.ps1') -CreateRootLauncher } 'retired' }
    Test-Case 'self-contained cannot be disabled for formal package' { Assert-Rejected { & (Join-Path $PSScriptRoot 'package-desktop.ps1') -SelfContained:$false } 'self-contained' }
    Test-Case 'valid desktop payload and exact runtime packs' { Assert-DesktopPublish (New-PublishFixture) win-x64 8.0.32 $false }
    Test-Case 'require third-party notices in published payload' { $f = New-PublishFixture; Remove-Item -LiteralPath (Join-Path $f 'THIRD-PARTY-NOTICES.txt'); Assert-Rejected { Assert-DesktopPublish $f win-x64 8.0.32 $false } 'notices' }
    Test-Case 'reject obsolete runtime manifest' {
        $p = New-PublishFixture; $file = Join-Path $p 'Xke.DesktopHost.deps.json'
        (Get-Content $file -Raw).Replace('8.0.32', '8.0.26') | Set-Content $file
        Assert-Rejected { Assert-DesktopPublish $p win-x64 8.0.32 $false } 'missing'
    }
    Test-Case 'reject framework-dependent runtimeconfig' {
        $p = New-PublishFixture; '{"runtimeOptions":{"frameworks":[]}}' | Set-Content (Join-Path $p 'Xke.DesktopHost.runtimeconfig.json')
        Assert-Rejected { Assert-DesktopPublish $p win-x64 8.0.32 $false } 'self-contained'
    }
    foreach ($bad in @('settings.json', 'xke.schedule-api.json', 'xke.schedule-api-token.json.dpapi', '.env', 'auth.log', 'Update.exe', 'Xke.Updater.dll')) {
        Test-Case "reject desktop extra $bad" {
            $p = New-PublishFixture; 'synthetic' | Set-Content (Join-Path $p $bad)
            Assert-Rejected { Assert-DesktopPublish $p win-x64 8.0.32 $false } 'Unexpected desktop'
        }
    }
    Test-Case 'required runtime availability is checked for all three packages' {
        $requests = [Collections.Generic.List[string]]::new()
        function Invoke-RestMethod { $requests.Add([string]$args[0]); return @{ versions = @('8.0.32') } }
        try {
            Assert-ReleaseRuntimeAvailable win-x64 8.0.32
            if ($requests.Count -ne 3 -or @($requests | Select-Object -Unique).Count -ne 3) { throw 'Not all runtime packs were checked.' }
        } finally { Remove-Item Function:\Invoke-RestMethod }
    }
    Test-Case 'missing required runtime patch is a hard block, not a fallback' {
        function Invoke-RestMethod { return @{ versions = @('8.0.26') } }
        try { Assert-Rejected { Assert-ReleaseRuntimeAvailable win-x64 8.0.32 } 'No older patch' }
        finally { Remove-Item Function:\Invoke-RestMethod }
    }
    Test-Case 'unverifiable runtime availability also blocks publishing' {
        function Invoke-RestMethod { throw 'Synthetic offline state.' }
        try { Assert-Rejected { Assert-ReleaseRuntimeAvailable win-x64 8.0.32 } 'no fallback' }
        finally { Remove-Item Function:\Invoke-RestMethod }
    }
    Test-Case 'pinned CLI output naming includes Windows suffix' {
        $n = Get-VelopackReleaseNames 1.0.0 win
        if ($n.Installer -cne 'XKE-win-Setup.exe' -or $n.Portable -cne 'XKE-win-Portable.zip' -or $n.Full -cne 'XKE-1.0.0-full.nupkg') { throw 'Wrong pinned release naming.' }
    }
    Test-Case 'validate complete synthetic Velopack release without executing it' { Assert-VelopackRelease (New-ReleaseFixture) 1.0.0 win $false }
    Test-Case 'accept pinned Velopack setup icon metadata' { Assert-VelopackRelease (New-ReleaseFixture -ExtraArchivePath 'setup.ico') 1.0.0 win $false }
    Test-Case 'validate explicit Windows channel naming' { Assert-VelopackRelease (New-ReleaseFixture win-x64) 1.0.0 win-x64 $false }
    Test-Case 'reject missing portable artifact' {
        $r = New-ReleaseFixture; Remove-Item -LiteralPath (Join-Path $r 'XKE-win-Portable.zip')
        Assert-Rejected { Assert-VelopackRelease $r 1.0.0 win $false } 'six-file'
    }
    Test-Case 'reject feed checksum mismatch' {
        $r = New-ReleaseFixture; Add-Content (Join-Path $r 'XKE-1.0.0-full.nupkg') 'tamper'
        Assert-Rejected { Assert-VelopackRelease $r 1.0.0 win $false } 'checksum'
    }
    foreach ($bad in @('lib/app/web/.env', 'lib/app/cache.json.dpapi', 'lib/app/xke.schedule-api.json', '../outside.txt', 'other/secret.json')) {
        Test-Case "reject packaged extra $bad" {
            $r = New-ReleaseFixture -ExtraArchivePath $bad
            Assert-Rejected { Assert-VelopackRelease $r 1.0.0 win $false } 'Unexpected|Unsafe'
        }
    }
    Test-Case 'OCR resource whitelist is exact' {
        if (-not (Test-DesktopPayloadPath 'ocr/common_old.onnx' $false) -or -not (Test-DesktopPayloadPath 'ocr/charset.json' $false)) { throw 'Pinned OCR resources rejected.' }
        foreach ($bad in @('ocr/cookies.json', 'ocr/debug.png', 'ocr/common.onnx', 'ocr/.env', 'ocr/source.py')) {
            if (Test-DesktopPayloadPath $bad $false) { throw "Unexpected OCR asset admitted: $bad" }
        }
    }
    Test-Case 'compiled OCR engine cannot ship without its verified assets' {
        $f = New-PublishFixture
        'synthetic' | Set-Content -LiteralPath (Join-Path $f 'Microsoft.ML.OnnxRuntime.dll')
        Assert-Rejected { Assert-DesktopPublish $f win-x64 8.0.32 $false } 'OCR asset'
    }
    foreach ($scenario in @('complete', 'complete-campus', 'private-publish-file', 'output-race')) {
        Test-Case "synthetic complete script flow: $scenario" {
            $frontend = New-FrontendFixture
            $final = Join-Path $root ('mock-output-' + [Guid]::NewGuid().ToString('N'))
            $mockParent = Join-Path $root ('mock-parent-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $mockParent | Out-Null
            $final = Join-Path $mockParent 'release'
            if ($scenario -eq 'complete') { New-Item -ItemType Directory -Path $final | Out-Null }
            function Invoke-RestMethod { return @{ versions = @('8.0.32') } }
            function dotnet {
                $arguments = @($args)
                $global:LASTEXITCODE = 0
                if ($arguments[0] -eq 'publish') {
                    $destination = $arguments[[Array]::IndexOf($arguments, '-o') + 1]
                    $fixture = New-PublishFixture
                    if ($scenario -eq 'private-publish-file') { 'synthetic' | Set-Content (Join-Path $fixture 'accounts.json') }
                    New-Item -ItemType Directory -Path $destination | Out-Null
                    foreach ($file in Get-ChildItem -LiteralPath $fixture -File) { Copy-Item -LiteralPath $file.FullName -Destination $destination }
                } elseif ($arguments -contains 'pack') {
                    $destination = $arguments[[Array]::IndexOf($arguments, '--outputDir') + 1]
                    $release = New-ReleaseFixture -IncludeCampusConfig:($scenario -eq 'complete-campus')
                    Copy-Item -LiteralPath $release -Destination $destination -Recurse
                    if ($scenario -eq 'complete-campus') {
                        $publishedConfig = Read-CampusReleaseConfig (Join-Path $arguments[[Array]::IndexOf($arguments, '--packDir') + 1] 'xke.campus-self-service.json')
                        if ($publishedConfig.Count -ne 2) { throw 'Unexpected campus release configuration fields.' }
                    }
                    if ($scenario -eq 'output-race') {
                        New-Item -ItemType Directory -Path $final | Out-Null
                        'preserve-me' | Set-Content (Join-Path $final 'concurrent.synthetic')
                    }
                } elseif ($arguments[0] -eq 'msbuild') {
                    # Download preparation is outside this synthetic transport fixture.
                } elseif ($arguments[0] -ne 'tool' -or $arguments[1] -ne 'restore') { throw 'Unexpected mocked command.' }
            }
            try {
                $packageArgs = @{ Version = '1.0.0'; WebRoot = $frontend; OutputDir = $final }
                if ($scenario -eq 'complete-campus') {
                    $campusInput = Join-Path $mockParent 'campus.local.synthetic.json'
                    '{"endpoint":"https://campus.example.invalid/Self","timeout_seconds":8}' | Set-Content -LiteralPath $campusInput
                    $packageArgs.CampusSelfServiceConfigPath = $campusInput
                }
                $action = { & (Join-Path $PSScriptRoot 'package-desktop.ps1') @packageArgs }
                if ($scenario -in @('complete', 'complete-campus')) {
                    & $action
                    Assert-VelopackRelease $final 1.0.0 win ($scenario -eq 'complete-campus')
                    if (@(Get-ChildItem -LiteralPath $mockParent -Directory -Force).Count -ne 1) { throw 'Successful staging was not cleaned.' }
                } elseif ($scenario -eq 'private-publish-file') {
                    Assert-Rejected $action 'Unexpected desktop publish file'
                    if (Test-Path -LiteralPath $final) { throw 'Failed validation created a final release.' }
                } else {
                    Assert-Rejected $action 'not empty'
                    if ((Get-Content (Join-Path $final 'concurrent.synthetic')) -cne 'preserve-me') { throw 'Concurrent output was modified.' }
                }
            } finally {
                Remove-Item Function:\dotnet, Function:\Invoke-RestMethod
            }
        }
    }
    Write-Host "Release gates: $script:passed passed. Synthetic-only; no publish/install/launch performed."
} finally {
    $resolvedRoot = [IO.Path]::GetFullPath($root)
    if ([IO.Path]::GetDirectoryName($resolvedRoot) -ne [IO.Path]::GetTempPath().TrimEnd('\', '/') -or
        [IO.Path]::GetFileName($resolvedRoot) -cnotmatch '^xke-release-tests-[0-9a-f]{32}$') { throw 'Unsafe synthetic fixture cleanup path.' }
    Assert-NoReleaseLinks $resolvedRoot
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
