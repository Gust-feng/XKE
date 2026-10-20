#Requires -Version 7.0
[CmdletBinding()]
param([string]$CacheDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'package-release-validation.ps1')
$repo = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$root = if ($CacheDirectory) { [IO.Path]::GetFullPath($CacheDirectory) } else { Join-Path $repo 'artifacts/captcha-assets' }
if (-not $root.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'OCR build assets must remain inside the workspace.' }
Assert-NoReleaseLinks $root
$lock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'captcha-assets.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force -Path (Join-Path $root 'ocr') | Out-Null
function Get-VerifiedAsset([string]$url, [string]$hash, [string]$path) {
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ieq $hash) { return }
    if (-not $url.StartsWith('https://')) { throw 'Build assets require HTTPS.' }
    $temp = "$path.download"
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            Invoke-WebRequest -Uri $url -OutFile $temp -TimeoutSec 120
            if ((Get-FileHash -LiteralPath $temp -Algorithm SHA256).Hash -ine $hash) { throw 'OCR source checksum mismatch.' }
            Move-Item -LiteralPath $temp -Destination $path -Force
            return
        } catch { if ($attempt -eq 3) { throw }; Start-Sleep -Seconds 2 }
    }
}
Get-VerifiedAsset $lock.model.url $lock.model.sha256 (Join-Path $root 'ocr/common_old.onnx')
$archive = Join-Path $root 'vc143-x64.vsix'
Get-VerifiedAsset $lock.crt.url $lock.crt.sha256 $archive
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($file in $lock.crt.files.PSObject.Properties) {
        $target = Join-Path $root $file.Name
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ine $file.Value) {
            $entry = $zip.GetEntry($lock.crt.entryPrefix + $file.Name)
            if ($null -eq $entry) { throw "Official CRT asset missing: $($file.Name)" }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ine $file.Value) { throw 'CRT checksum mismatch.' }
        $signature = Get-AuthenticodeSignature -LiteralPath $target
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Microsoft Corporation*') { throw 'CRT is not validly Microsoft-signed.' }
    }
} finally { $zip.Dispose() }
@"
Microsoft Visual C++ 2022 Redistributable, x64, 14.44.35211.
Copyright (c) Microsoft Corporation. All rights reserved.
App-local files are taken only from the official Visual Studio manifest's
VC/Redist/MSVC/14.44.35112/x64/Microsoft.VC143.CRT redistributable directory.
No debug_nonredist files or developer machine System32 files are included.
Redistribution list: https://learn.microsoft.com/visualstudio/releases/2022/redistribution
License terms: https://visualstudio.microsoft.com/license-terms/vs2022-cruntime/
"@ | Set-Content -LiteralPath (Join-Path $root 'ocr/VC-RUNTIME-NOTICE.txt') -Encoding utf8
Write-Host 'Verified default OCR model and Microsoft-signed app-local x64 CRT assets.'
