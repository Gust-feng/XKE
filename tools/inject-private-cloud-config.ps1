param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [string]$PrivateConfigPath,

    [string]$OutputFileName = "xke.runtime.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PrivateConfigPath)) {
    $PrivateConfigPath = Join-Path $repoRoot "开发数据\dotnet8-cloud.local.json"
}

function Resolve-FullPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Read-StringValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return ""
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return ""
    }

    return "$($property.Value)".Trim()
}

function Read-NestedStringValue {
    param(
        [object]$Object,
        [string]$ObjectName,
        [string]$Name
    )

    $child = $Object.PSObject.Properties[$ObjectName]
    if ($null -eq $child) {
        return ""
    }

    return Read-StringValue -Object $child.Value -Name $Name
}

function Select-FirstNonEmpty {
    param([string[]]$Values)

    foreach ($value in $Values) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value.Trim()
        }
    }

    return ""
}

function Normalize-NoticeEndpoint {
    param([string]$Value)

    $trimmed = $Value.Trim()
    if ($trimmed -match "/cloud-feed\.json($|[?#])") {
        return $trimmed
    }

    if ($trimmed.EndsWith("/")) {
        return $trimmed
    }

    return "$trimmed/"
}

$resolvedPublishDir = Resolve-FullPath $PublishDir
$resolvedPrivateConfigPath = Resolve-FullPath $PrivateConfigPath

if (-not (Test-Path -LiteralPath $resolvedPublishDir -PathType Container)) {
    throw "PublishDir does not exist: $resolvedPublishDir"
}

if (-not (Test-Path -LiteralPath $resolvedPrivateConfigPath -PathType Leaf)) {
    throw "Private config does not exist: $resolvedPrivateConfigPath"
}

$privateConfig = Get-Content -LiteralPath $resolvedPrivateConfigPath -Raw | ConvertFrom-Json
$noticeEndpoint = Select-FirstNonEmpty @(
    (Read-NestedStringValue -Object $privateConfig -ObjectName "remote" -Name "n"),
    (Read-NestedStringValue -Object $privateConfig -ObjectName "remote" -Name "notice"),
    (Read-StringValue -Object $privateConfig -Name "notice_base_url"),
    (Read-StringValue -Object $privateConfig -Name "cloud_notice_base_url"),
    (Read-StringValue -Object $privateConfig -Name "cos_base_url"),
    (Read-StringValue -Object $privateConfig -Name "cloud_feed_base_url"),
    (Read-NestedStringValue -Object $privateConfig -ObjectName "notice" -Name "base_url"),
    (Read-NestedStringValue -Object $privateConfig -ObjectName "notice" -Name "cos_base_url")
)
$telemetryEndpoint = Select-FirstNonEmpty @(
    (Read-NestedStringValue -Object $privateConfig -ObjectName "remote" -Name "t"),
    (Read-NestedStringValue -Object $privateConfig -ObjectName "remote" -Name "report"),
    (Read-StringValue -Object $privateConfig -Name "telemetry_cloud_endpoint"),
    (Read-StringValue -Object $privateConfig -Name "telemetry_base_url"),
    (Read-StringValue -Object $privateConfig -Name "cloud_function_base_url"),
    (Read-NestedStringValue -Object $privateConfig -ObjectName "telemetry" -Name "endpoint"),
    (Read-NestedStringValue -Object $privateConfig -ObjectName "telemetry" -Name "base_url"),
    (Read-NestedStringValue -Object $privateConfig -ObjectName "cloud_function" -Name "base_url")
)

if ([string]::IsNullOrWhiteSpace($noticeEndpoint)) {
    throw "Private config is missing the notice endpoint."
}

if ([string]::IsNullOrWhiteSpace($telemetryEndpoint)) {
    throw "Private config is missing the report endpoint."
}

$outputPath = Join-Path $resolvedPublishDir $OutputFileName
$payload = [ordered]@{
    schema_version = 1
    generated_at = [DateTimeOffset]::Now.ToString("O")
    remote = [ordered]@{
        n = Normalize-NoticeEndpoint $noticeEndpoint
        t = $telemetryEndpoint.Trim()
    }
}

$json = $payload | ConvertTo-Json -Depth 6
Set-Content -LiteralPath $outputPath -Value $json -Encoding UTF8

Write-Host "Injected private runtime config into publish directory."
Write-Host "Output: $outputPath"
Write-Host "Notice endpoint: set"
Write-Host "Report endpoint: set"
