# Shared release gates. Dot-sourcing this file performs no publishing or network requests.
# CLI/naming contract: Velopack tag 1.2.161 (DefaultName.cs), not historical Squirrel tooling.

function Resolve-ReleasePath {
    param([string]$Path, [string]$BaseDirectory)
    return [IO.Path]::GetFullPath($Path, $BaseDirectory)
}

function Assert-NoReleaseLinks {
    param([string]$Path)
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if ((Test-Path -LiteralPath $current) -and
            ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Release inputs and outputs cannot use symbolic links or junctions: $current"
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

function Get-ReleaseFiles {
    param([string]$Root)
    Assert-NoReleaseLinks $Root
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Release directory not found: $Root" }
    # Inspect directories before descending so a junction cannot escape the selected root.
    foreach ($item in Get-ChildItem -LiteralPath $Root -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked release entry is forbidden: $($item.FullName)" }
        if ($item.PSIsContainer) { Get-ReleaseFiles $item.FullName } else { $item }
    }
}

function Read-ReleaseJsonObject {
    param([string]$Path)
    try { $value = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "Invalid or missing release JSON: $Path" }
    if ($value -isnot [pscustomobject]) { throw "Release JSON must be an object: $Path" }
    return $value
}

function Get-DesktopReleaseVersion {
    param([string]$RepositoryRoot, [string]$Version)
    if ([string]::IsNullOrWhiteSpace($Version)) {
        [xml]$props = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'Directory.Build.props') -Raw
        $versions = @($props.SelectNodes('/Project/PropertyGroup/Version'))
        if ($versions.Count -ne 1) { throw 'Directory.Build.props must define one main Version for formal packaging.' }
        $Version = $versions[0].InnerText
    }
    if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw 'Formal release version must be a stable three-part version, for example 1.0.0 (without V prefix).'
    }
    return $Version
}

function Assert-EmptyReleaseOutput {
    param([string]$Path)
    Assert-NoReleaseLinks $Path
    if (Test-Path -LiteralPath $Path) {
        if (-not (Test-Path -LiteralPath $Path -PathType Container)) { throw 'Release output must be a directory.' }
        if (@(Get-ChildItem -LiteralPath $Path -Force).Count -gt 0) {
            throw "Release output is not empty; existing packages are never overwritten: $Path"
        }
    }
}

function Test-FrontendReleasePath {
    param([string]$RelativePath)
    # Vite's emitted hashed resources only; no arbitrary JSON, source maps or copied public directories.
    return $RelativePath -cin @('index.html', 'frontend-build.json', 'favicon.svg', 'favicon.ico') -or
        $RelativePath -cmatch '^assets/[A-Za-z0-9_.-]+-[A-Za-z0-9_-]{8,}\.(js|css|woff2?|ttf|otf|eot|svg|png|jpe?g|gif|webp|avif|ico|wasm|mp4|webm|mp3|ogg)$'
}

function Assert-FrontendRuntime {
    param([string]$SourceRoot)
    $SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
    $files = @(Get-ReleaseFiles $SourceRoot)
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($SourceRoot, $file.FullName).Replace('\', '/')
        if (-not (Test-FrontendReleasePath $relative)) { throw "Unexpected frontend release file: $relative" }
    }
    $metadata = Read-ReleaseJsonObject (Join-Path $SourceRoot 'frontend-build.json')
    if (@($metadata.PSObject.Properties.Name | Where-Object { $_ -cnotin @('mode', 'build') }).Count -ne 0 -or
        $metadata.mode -cne 'runtime' -or $metadata.build -isnot [string] -or $metadata.build -cnotmatch '^[0-9a-f]{16}$') {
        throw 'Every WebRoot requires valid runtime mode and a Vite build fingerprint; preview/legacy metadata is forbidden.'
    }
    $index = Get-Content -LiteralPath (Join-Path $SourceRoot 'index.html') -Raw
    $references = [regex]::Matches($index, '(?i)\b(?:src|href)\s*=\s*(?:"(?<path>[^"]*)"|''(?<path>[^'']*)''|(?<unquoted>[^\s>]+))')
    $hasScript = $false
    foreach ($reference in $references) {
        if ($reference.Groups['unquoted'].Success) { throw 'Frontend index resource references must be quoted.' }
        $path = $reference.Groups['path'].Value
        if ($path -match '^(?:\./)?(?:assets/|favicon\.(?:svg|ico)$)') { $path = $path -replace '^\./', '' }
        else { throw "Frontend index contains a non-local or unsupported reference: $path" }
        if (-not (Test-FrontendReleasePath $path) -or -not (Test-Path -LiteralPath (Join-Path $SourceRoot $path) -PathType Leaf)) {
            throw "Frontend index references a missing or forbidden resource: $path"
        }
    }
    foreach ($tag in [regex]::Matches($index, '(?is)<script\b(?<attributes>[^>]*)>')) {
        $attributes = $tag.Groups['attributes'].Value
        $module = [regex]::IsMatch($attributes, '(?i)(?:^|\s)type\s*=\s*(?:"module"|''module'')(?=\s|$)')
        $source = [regex]::Match($attributes, '(?i)(?:^|\s)src\s*=\s*(?:"(?<path>[^"]*)"|''(?<path>[^'']*)'')')
        if ($module -and $source.Success -and $source.Groups['path'].Value.EndsWith('.js', [StringComparison]::Ordinal)) {
            $hasScript = $true
        }
    }
    if (-not $hasScript) { throw 'Frontend index has no runtime JavaScript module entry.' }
    return $files
}

function Read-CampusReleaseConfig {
    param([string]$Path)
    $config = Read-ReleaseJsonObject $Path
    if (@($config.PSObject.Properties.Name | Where-Object { $_ -cnotin @('endpoint', 'timeout_seconds') }).Count -ne 0) {
        throw 'Campus release configuration permits endpoint and timeout_seconds only; credentials/tokens are forbidden.'
    }
    $uri = $null
    if ($config.endpoint -isnot [string] -or -not [Uri]::TryCreate($config.endpoint, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -notin @('http', 'https') -or $uri.AbsolutePath.TrimEnd('/') -cne '/Self' -or
        $uri.UserInfo.Length -gt 0 -or $uri.Query.Length -gt 0 -or $uri.Fragment.Length -gt 0) {
        throw 'Campus endpoint must be an HTTP(S) /Self URL without credentials, query parameters or fragments.'
    }
    $timeout = 8
    if ($config.PSObject.Properties.Name -ccontains 'timeout_seconds') {
        $timeout = $config.timeout_seconds
        if (($timeout -isnot [int] -and $timeout -isnot [long]) -or $timeout -lt 1 -or $timeout -gt 60) {
            throw 'Campus timeout_seconds must be an integer between 1 and 60.'
        }
    }
    # Construct a new object; never copy an input file or a development directory into the release.
    return [ordered]@{ endpoint = $uri.AbsoluteUri; timeout_seconds = [int]$timeout }
}

function Get-RequiredRuntimePacks {
    param([string]$RuntimeIdentifier)
    return @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App', 'Microsoft.AspNetCore.App') |
        ForEach-Object { "$_.Runtime.$RuntimeIdentifier" }
}

function Assert-ReleaseRuntimeAvailable {
    param([string]$RuntimeIdentifier, [string]$RuntimeVersion)
    foreach ($package in Get-RequiredRuntimePacks $RuntimeIdentifier) {
        try { $index = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$($package.ToLowerInvariant())/index.json" -TimeoutSec 30 }
        catch { throw "Cannot verify required runtime pack $package/$RuntimeVersion on NuGet; publishing stopped (no fallback)." }
        if ($index.versions -cnotcontains $RuntimeVersion) { throw "Required runtime pack is unavailable: $package/$RuntimeVersion. No older patch is permitted." }
    }
}

function Test-DesktopPayloadPath {
    param([string]$RelativePath, [bool]$IncludeCampusConfig)
    if ($RelativePath -cin @('ocr/common_old.onnx', 'ocr/charset.json', 'ocr/DDDDOCR-LICENSE.txt', 'ocr/PILLOW-LICENSE.txt', 'ocr/VC-RUNTIME-NOTICE.txt', 'ocr/ONNXRUNTIME-LICENSE.txt', 'ocr/ONNXRUNTIME-NOTICES.txt', 'ocr/TENSORS-LICENSE.txt', 'ocr/TENSORS-NOTICES.txt')) { return $true }
    if ($RelativePath.StartsWith('web/', [StringComparison]::Ordinal)) { return Test-FrontendReleasePath $RelativePath.Substring(4) }
    if ($RelativePath -cin @('Xke.DesktopHost.exe', 'Xke.DesktopHost.deps.json', 'Xke.DesktopHost.runtimeconfig.json', 'THIRD-PARTY-NOTICES.txt')) { return $true }
    if ($IncludeCampusConfig -and $RelativePath -ceq 'xke.campus-self-service.json') { return $true }
    # Only fresh dotnet publish binaries/resources are admitted. Runtime data formats have no allow rule.
    return $RelativePath -cmatch '^(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.dll$' -and
        $RelativePath -notmatch '(?i)(^|/)(?:Xke\.Updater|Squirrel|Update)(?:\.|/)'
}

function Assert-DesktopPublish {
    param([string]$PublishRoot, [string]$RuntimeIdentifier, [string]$RuntimeVersion, [bool]$IncludeCampusConfig)
    foreach ($file in Get-ReleaseFiles $PublishRoot) {
        $relative = [IO.Path]::GetRelativePath($PublishRoot, $file.FullName).Replace('\', '/')
        if (-not (Test-DesktopPayloadPath $relative $IncludeCampusConfig)) { throw "Unexpected desktop publish file: $relative" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $PublishRoot 'Xke.DesktopHost.exe') -PathType Leaf)) { throw 'Published desktop entry executable is missing.' }
    if (-not (Test-Path -LiteralPath (Join-Path $PublishRoot 'THIRD-PARTY-NOTICES.txt') -PathType Leaf)) { throw 'Third-party notices are missing.' }
    if (Test-Path -LiteralPath (Join-Path $PublishRoot 'Microsoft.ML.OnnxRuntime.dll')) { Assert-CaptchaPayload $PublishRoot }
    $deps = Read-ReleaseJsonObject (Join-Path $PublishRoot 'Xke.DesktopHost.deps.json')
    foreach ($package in Get-RequiredRuntimePacks $RuntimeIdentifier) {
        $key = "runtimepack.$package/$RuntimeVersion"
        if ($deps.libraries.PSObject.Properties.Name -cnotcontains $key) { throw "Published dependency manifest is missing $key." }
        $versions = @($deps.libraries.PSObject.Properties.Name | Where-Object { $_.StartsWith("runtimepack.$package/", [StringComparison]::Ordinal) })
        if ($versions.Count -ne 1) { throw "Published dependency manifest contains ambiguous runtime versions for $package." }
    }
    if ($deps.libraries.PSObject.Properties.Name -cnotcontains 'Velopack/1.2.161') { throw 'Desktop SDK and local vpk must both be pinned to Velopack 1.2.161.' }
    $runtime = Read-ReleaseJsonObject (Join-Path $PublishRoot 'Xke.DesktopHost.runtimeconfig.json')
    foreach ($name in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App', 'Microsoft.AspNetCore.App')) {
        $frameworks = @($runtime.runtimeOptions.includedFrameworks | Where-Object { $_.name -ceq $name })
        if ($frameworks.Count -ne 1 -or $frameworks[0].version -cne $RuntimeVersion) { throw "Published self-contained runtime must include $name $RuntimeVersion." }
    }
    $null = Assert-FrontendRuntime (Join-Path $PublishRoot 'web')
    if ($IncludeCampusConfig) { $null = Read-CampusReleaseConfig (Join-Path $PublishRoot 'xke.campus-self-service.json') }
}

function Get-VelopackReleaseNames {
    param([string]$Version, [string]$Channel)
    # Confirmed against the pinned CLI's DefaultName.cs: Setup/Portable DO include '-win'.
    $full = if ($Channel -ceq 'win') { "XKE-$Version-full.nupkg" } else { "XKE-$Version-$Channel-full.nupkg" }
    $legacy = if ($Channel -ceq 'win') { 'RELEASES' } else { "RELEASES-$Channel" }
    return [ordered]@{
        Installer = "XKE-$Channel-Setup.exe"; Portable = "XKE-$Channel-Portable.zip"; Full = $full
        Feed = "releases.$Channel.json"; Assets = "assets.$Channel.json"; Legacy = $legacy
    }
}

function Assert-VelopackArchive {
    param([string]$Path, [ValidateSet('Full', 'Portable')][string]$Kind, [bool]$IncludeCampusConfig)
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $prefix = if ($Kind -eq 'Full') { 'lib/app/' } else { 'current/' }
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ($name -match '(^/|\\|(^|/)\.\.?(/|$)|:)' -or -not $seen.Add($name)) { throw "Unsafe or duplicate archive entry: $name" }
            if ($name.EndsWith('/')) { continue }
            if ($name.StartsWith($prefix, [StringComparison]::Ordinal)) {
                $relative = $name.Substring($prefix.Length)
                $generated = @('sq.version')
                if ($Kind -eq 'Full') { $generated += @('Squirrel.exe', 'XKE_ExecutionStub.exe') }
                if ($relative -cnotin $generated -and -not (Test-DesktopPayloadPath $relative $IncludeCampusConfig)) {
                    throw "Unexpected application file in $Kind archive: $name"
                }
            } elseif ($Kind -eq 'Full') {
                if ($name -cnotin @('XKE.nuspec', '[Content_Types].xml', '_rels/.rels', 'setup.ico')) { throw "Unexpected NuGet metadata entry: $name" }
            } elseif ($name -cnotin @('Update.exe', 'XKE.exe', '.portable')) { throw "Unexpected portable root entry: $name" }
        }
        foreach ($required in @("${prefix}Xke.DesktopHost.exe", "${prefix}Xke.DesktopHost.dll", "${prefix}Velopack.dll", "${prefix}Xke.DesktopHost.deps.json", "${prefix}Xke.DesktopHost.runtimeconfig.json", "${prefix}sq.version", "${prefix}web/index.html", "${prefix}web/frontend-build.json", "${prefix}THIRD-PARTY-NOTICES.txt")) {
            if (-not $seen.Contains($required)) { throw "Archive is missing $required." }
        }
        if ($seen.Contains("${prefix}Microsoft.ML.OnnxRuntime.dll")) {
            $hashes = Get-CaptchaReleaseHashes
            foreach ($pair in $hashes.GetEnumerator()) {
                $entry = $zip.GetEntry($prefix + $pair.Key)
                if ($null -eq $entry) { throw "OCR package asset missing: $($pair.Key)" }
                $stream = $entry.Open()
                try { $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
                if ($actual -ine $pair.Value) { throw "OCR package asset hash mismatch: $($pair.Key)" }
            }
            foreach ($name in Get-CaptchaNoticePaths) { if (-not $seen.Contains($prefix + $name)) { throw "OCR notice missing: $name" } }
        }
        if ($Kind -eq 'Portable') {
            foreach ($required in @('Update.exe', 'XKE.exe', '.portable')) {
                if (-not $seen.Contains($required)) { throw "Portable archive is missing $required." }
            }
        }
    } finally { $zip.Dispose() }
}

function Assert-VelopackRelease {
    param([string]$ReleaseRoot, [string]$Version, [string]$Channel, [bool]$IncludeCampusConfig)
    $names = Get-VelopackReleaseNames $Version $Channel
    $files = @(Get-ReleaseFiles $ReleaseRoot)
    if ($files.Count -ne $names.Count) { throw 'Velopack output must contain the complete six-file release set, with no extra files.' }
    foreach ($file in $files) {
        if ($file.Name -cnotin $names.Values -or $file.DirectoryName -ne [IO.Path]::GetFullPath($ReleaseRoot) -or $file.Length -eq 0) {
            throw "Unexpected or empty Velopack output file: $($file.Name)"
        }
    }
    $feed = Read-ReleaseJsonObject (Join-Path $ReleaseRoot $names.Feed)
    $assets = @($feed.Assets)
    if ($assets.Count -ne 1 -or $assets[0].PackageId -cne 'XKE' -or $assets[0].Version -cne $Version -or
        $assets[0].Type -cne 'Full' -or $assets[0].FileName -cne $names.Full) { throw 'Release feed does not identify the expected XKE full package/version.' }
    $package = Join-Path $ReleaseRoot $names.Full
    if ($assets[0].Size -ne (Get-Item -LiteralPath $package).Length -or
        $assets[0].SHA256 -ine (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -or
        $assets[0].SHA1 -ine (Get-FileHash -LiteralPath $package -Algorithm SHA1).Hash) { throw 'Release feed package size or checksum does not match.' }
    $buildAssets = @(Get-Content -LiteralPath (Join-Path $ReleaseRoot $names.Assets) -Raw | ConvertFrom-Json)
    if ($buildAssets.Count -ne 3) { throw 'Velopack asset list must contain Full, Portable and Installer.' }
    foreach ($type in @('Full', 'Portable', 'Installer')) {
        $asset = @($buildAssets | Where-Object { $_.Type -ceq $type -and $_.RelativeFileName -ceq $names[$type] })
        if ($asset.Count -ne 1) { throw "Velopack asset list has no unique $type artifact." }
    }
    Assert-VelopackArchive $package Full $IncludeCampusConfig
    Assert-VelopackArchive (Join-Path $ReleaseRoot $names.Portable) Portable $IncludeCampusConfig
    $zip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $reader = [IO.StreamReader]::new($zip.GetEntry('XKE.nuspec').Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        foreach ($pair in @(@('id', 'XKE'), @('version', $Version), @('channel', $Channel), @('mainExe', 'Xke.DesktopHost.exe'), @('runtimeDependencies', 'webview2'))) {
            $node = $nuspec.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='$($pair[0])']")
            if ($null -eq $node -or $node.InnerText -cne $pair[1]) { throw "Unexpected Velopack package metadata: $($pair[0])" }
        }
    } finally { $zip.Dispose() }
}


function Get-CaptchaNoticePaths {
    return @('ocr/DDDDOCR-LICENSE.txt', 'ocr/PILLOW-LICENSE.txt', 'ocr/VC-RUNTIME-NOTICE.txt', 'ocr/ONNXRUNTIME-LICENSE.txt', 'ocr/ONNXRUNTIME-NOTICES.txt', 'ocr/TENSORS-LICENSE.txt', 'ocr/TENSORS-NOTICES.txt')
}
function Get-CaptchaReleaseHashes {
    $lock = Read-ReleaseJsonObject (Join-Path $PSScriptRoot 'captcha-assets.lock.json')
    $hashes = @{ 'ocr/common_old.onnx' = $lock.model.sha256; 'ocr/charset.json' = $lock.charsetSha256 }
    foreach ($file in $lock.crt.files.PSObject.Properties) { $hashes[$file.Name] = $file.Value }
    foreach ($file in $lock.engineFiles.PSObject.Properties) { $hashes[$file.Name] = $file.Value }
    return $hashes
}
function Assert-CaptchaPayload {
    param([string]$PublishRoot)
    foreach ($pair in (Get-CaptchaReleaseHashes).GetEnumerator()) {
        $path = Join-Path $PublishRoot $pair.Key
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "OCR asset missing: $($pair.Key)" }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $pair.Value) { throw "OCR asset hash mismatch: $($pair.Key)" }
    }
    foreach ($name in Get-CaptchaNoticePaths) {
        $path = Join-Path $PublishRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) { throw "OCR notice missing: $name" }
    }
}
