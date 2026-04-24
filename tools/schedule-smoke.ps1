param(
    [string]$ProjectPath = "",
    [switch]$ManualMobile,
    [string]$User = "",
    [string]$PreloginMode = "coded",
    [switch]$KeepDataRoot
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function ConvertTo-PlainText {
    param([Security.SecureString]$SecureString)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Set-InheritedEnvVar {
    param(
        [string]$Name,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        Remove-Item "Env:$Name" -ErrorAction SilentlyContinue
        return
    }

    Set-Item "Env:$Name" $Value
}

function Get-FreeTcpPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return $listener.LocalEndpoint.Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-ForHealth {
    param(
        [string]$BaseUrl,
        [System.Diagnostics.Process]$Process,
        [string]$StdoutPath,
        [string]$StderrPath
    )

    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) {
            $stdout = if (Test-Path $StdoutPath) { Get-Content $StdoutPath -Raw } else { "" }
            $stderr = if (Test-Path $StderrPath) { Get-Content $StderrPath -Raw } else { "" }
            throw "WebHost exited before health check succeeded.`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
        }

        try {
            $null = Invoke-RestMethod -Uri "$BaseUrl/api/health" -Method Get -TimeoutSec 5
            return
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    $stdout = if (Test-Path $StdoutPath) { Get-Content $StdoutPath -Raw } else { "" }
    $stderr = if (Test-Path $StderrPath) { Get-Content $StderrPath -Raw } else { "" }
    throw "Timed out waiting for WebHost health check.`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
}

function Start-SmokeHost {
    param(
        [string]$Project,
        [string]$DataRoot,
        [string]$WebRoot
    )

    $port = Get-FreeTcpPort
    $baseUrl = "http://127.0.0.1:$port"
    $stdoutPath = Join-Path $DataRoot "webhost-$port.stdout.log"
    $stderrPath = Join-Path $DataRoot "webhost-$port.stderr.log"

    $previousUrls = $env:ASPNETCORE_URLS
    $previousDataRoot = $env:XKE_DOTNET_DATA_DIR
    $previousWebRoot = $env:XKE_WEB_ROOT

    try {
        Set-InheritedEnvVar -Name "ASPNETCORE_URLS" -Value $baseUrl
        Set-InheritedEnvVar -Name "XKE_DOTNET_DATA_DIR" -Value $DataRoot
        Set-InheritedEnvVar -Name "XKE_WEB_ROOT" -Value $WebRoot

        $process = Start-Process -FilePath "dotnet" `
            -ArgumentList @("run", "--project", $Project, "--no-launch-profile") `
            -WorkingDirectory $repoRoot `
            -PassThru `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath
    }
    finally {
        Set-InheritedEnvVar -Name "ASPNETCORE_URLS" -Value $previousUrls
        Set-InheritedEnvVar -Name "XKE_DOTNET_DATA_DIR" -Value $previousDataRoot
        Set-InheritedEnvVar -Name "XKE_WEB_ROOT" -Value $previousWebRoot
    }

    Wait-ForHealth -BaseUrl $baseUrl -Process $process -StdoutPath $stdoutPath -StderrPath $stderrPath
    return [pscustomobject]@{
        Process = $process
        BaseUrl = $baseUrl
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
    }
}

function Stop-SmokeHost {
    param($HostInfo)

    if ($null -eq $HostInfo -or $null -eq $HostInfo.Process) {
        return
    }

    $process = $HostInfo.Process
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        try {
            Wait-Process -Id $process.Id -Timeout 10 -ErrorAction Stop
        }
        catch {
        }
    }
}

function New-ApiClient {
    param([string]$BaseUrl)

    $client = [System.Net.Http.HttpClient]::new()
    $client.BaseAddress = [Uri]$BaseUrl
    return $client
}

function Invoke-Api {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Method,
        [string]$Path,
        $Body = $null,
        [hashtable]$Headers = $null
    )

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Path)
    try {
        if ($Headers) {
            foreach ($entry in $Headers.GetEnumerator()) {
                $request.Headers.TryAddWithoutValidation([string]$entry.Key, [string]$entry.Value) | Out-Null
            }
        }

        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 10 -Compress
            $request.Content = [System.Net.Http.StringContent]::new($json, [Text.Encoding]::UTF8, "application/json")
        }

        $response = $Client.SendAsync($request).GetAwaiter().GetResult()
        $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $parsed = $null
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            $parsed = $text | ConvertFrom-Json
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Text = $text
            Body = $parsed
        }
    }
    finally {
        $request.Dispose()
    }
}

function Assert-ApiSuccess {
    param(
        $Response,
        [string]$Operation
    )

    $message = ""
    if ($Response.Body -and $Response.Body.msg) {
        $message = [string]$Response.Body.msg
    }
    elseif ($Response.Body -and $Response.Body.detail) {
        $message = [string]$Response.Body.detail
    }

    if ($Response.StatusCode -lt 200 -or $Response.StatusCode -ge 300) {
        throw "$Operation failed with HTTP $($Response.StatusCode): $message"
    }

    if ($Response.Body -and $null -ne $Response.Body.ok -and -not [bool]$Response.Body.ok) {
        throw "$Operation failed: $message"
    }
}

if (-not $ManualMobile) {
    if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
        $ProjectPath = Join-Path $PSScriptRoot "..\tests\Xke.WebHost.Tests\Xke.WebHost.Tests.csproj"
    }

    $resolvedProject = (Resolve-Path $ProjectPath).Path
    Write-Host "Running schedule smoke tests against $resolvedProject"

    dotnet test $resolvedProject --filter "FullyQualifiedName~Schedule"
    exit $LASTEXITCODE
}

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $PSScriptRoot "..\src\Xke.WebHost\Xke.WebHost.csproj"
}

$resolvedProject = (Resolve-Path $ProjectPath).Path
$resolvedWebRoot = Join-Path $repoRoot "..\web"
if (Test-Path $resolvedWebRoot) {
    $resolvedWebRoot = (Resolve-Path $resolvedWebRoot).Path
}
else {
    $resolvedWebRoot = ""
}

if ([string]::IsNullOrWhiteSpace($User)) {
    $User = Read-Host "Student ID"
}

$securePassword = Read-Host "Password" -AsSecureString
$password = ConvertTo-PlainText $securePassword

$dataRoot = Join-Path ([IO.Path]::GetTempPath()) ("xke-dotnet8-schedule-smoke-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $dataRoot | Out-Null

$hostInfo = $null
$restartHostInfo = $null
$client = $null
$restartClient = $null

try {
    Write-Host "Starting WebHost for manual mobile schedule smoke..."
    Write-Host "Data root: $dataRoot"
    $hostInfo = Start-SmokeHost -Project $resolvedProject -DataRoot $dataRoot -WebRoot $resolvedWebRoot
    Write-Host "Base URL: $($hostInfo.BaseUrl)"

    $client = New-ApiClient -BaseUrl $hostInfo.BaseUrl

    $login = Invoke-Api -Client $client -Method "POST" -Path "/api/auth/login" -Body @{
        user = $User
        password = $password
        prelogin_mode = $PreloginMode
    }
    Assert-ApiSuccess -Response $login -Operation "auth/login"

    $sessionId = [string]$login.Body.data.session_id
    $connected = [bool]$login.Body.data.connected
    $cachedSchedule = $login.Body.data.cached_schedule
    Write-Host "Login OK. Session: $sessionId"
    Write-Host "Connected: $connected"
    if ($null -eq $cachedSchedule) {
        throw "auth/login did not return cached_schedule from first initialization"
    }
    if ($null -eq $cachedSchedule.rows -or [int]$cachedSchedule.rows.Count -le 0) {
        throw "auth/login cached_schedule did not contain usable schedule rows"
    }
    Write-Host ("First initialization schedule OK. Week: {0}, Rows: {1}" -f `
        [string]$cachedSchedule.week_label, `
        [int]$cachedSchedule.rows.Count)

    $refresh = Invoke-Api -Client $client -Method "POST" -Path "/api/schedule/preferred?session_id=$sessionId" -Body @{
        mode = "current"
    }
    Assert-ApiSuccess -Response $refresh -Operation "schedule/preferred"
    Write-Host "Preferred refresh OK."
    Write-Host ("Source: {0}, Week: {1}, Rows: {2}" -f `
        [string]$refresh.Body.data.source_type, `
        [string]$refresh.Body.data.week_label, `
        [int]$refresh.Body.data.rows.Count)

    $local = Invoke-Api -Client $client -Method "GET" -Path "/api/schedule/local?source=preferred" -Headers @{
        "X-XKE-Session" = $sessionId
    }
    Assert-ApiSuccess -Response $local -Operation "schedule/local"
    if (-not [bool]$local.Body.data.cached) {
        throw "schedule/local did not return cached=true"
    }
    Write-Host "Local schedule load OK."

    Stop-SmokeHost -HostInfo $hostInfo
    $hostInfo = $null

    Write-Host "Restarting WebHost to verify local session restore..."
    $restartHostInfo = Start-SmokeHost -Project $resolvedProject -DataRoot $dataRoot -WebRoot $resolvedWebRoot
    $restartClient = New-ApiClient -BaseUrl $restartHostInfo.BaseUrl

    $session = Invoke-Api -Client $restartClient -Method "GET" -Path "/api/auth/session" -Headers @{
        "X-XKE-Session" = $sessionId
    }
    Assert-ApiSuccess -Response $session -Operation "auth/session after restart"
    if ([bool]$session.Body.data.connected) {
        throw "auth/session should restore with connected=false after restart"
    }
    Write-Host "Session restore OK. Connected reset to false as expected."

    $localAfterRestart = Invoke-Api -Client $restartClient -Method "GET" -Path "/api/schedule/local?source=preferred" -Headers @{
        "X-XKE-Session" = $sessionId
    }
    Assert-ApiSuccess -Response $localAfterRestart -Operation "schedule/local after restart"
    if (-not [bool]$localAfterRestart.Body.data.cached) {
        throw "schedule/local after restart did not return cached=true"
    }
    Write-Host "Local cache restore after restart OK."
    Write-Host "Manual mobile schedule smoke completed successfully."
}
finally {
    if ($client) {
        $client.Dispose()
    }
    if ($restartClient) {
        $restartClient.Dispose()
    }

    Stop-SmokeHost -HostInfo $hostInfo
    Stop-SmokeHost -HostInfo $restartHostInfo

    if (-not $KeepDataRoot -and (Test-Path $dataRoot)) {
        Remove-Item -LiteralPath $dataRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
