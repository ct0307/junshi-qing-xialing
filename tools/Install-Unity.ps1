param([string]$InstallRoot = 'F:\Development\Unity')
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$downloadRoot = Join-Path $InstallRoot 'Downloads'
New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
$statusPath = Join-Path $downloadRoot 'install-status.json'
$logPath = Join-Path $downloadRoot 'install.log'

function Write-InstallStatus([string]$stage, [string]$message) {
    @{stage=$stage;message=$message;updated=(Get-Date).ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
    Add-Content -LiteralPath $logPath -Value ((Get-Date).ToString('s') + ' ' + $stage + ' ' + $message) -Encoding UTF8
}

function Get-OfficialFile([string]$url, [string]$target) {
    if (Test-Path -LiteralPath $target) { return }
    $partial = $target + '.partial'
    & curl.exe --location --fail --retry 2 --connect-timeout 20 --silent --show-error --output $partial $url
    if ($LASTEXITCODE -ne 0) { throw ('Download failed: ' + $url) }
    Move-Item -LiteralPath $partial -Destination $target
}

function Assert-UnitySignature([string]$path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    # 官方中国下载节点的发行者使用中文公司名，签名仍需系统验证有效。
    $officialPublisher = 'Unity Technologies|\u4f18\u4e09\u7f14\u79d1\u6280'
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch $officialPublisher) {
        throw ('Official signature check failed: ' + $path + ' ' + $signature.Status)
    }
}

try {
    $version = '6000.3.28f1'
    $release = Invoke-RestMethod -Uri ('https://services.api.unity.com/unity/editor/release/v1/releases?version=' + $version + '&limit=1&platform=WINDOWS')
    $entry = $release.results | Where-Object {$_.version -eq $version} | Select-Object -First 1
    $editor = $entry.downloads | Where-Object {$_.platform -eq 'WINDOWS' -and $_.architecture -eq 'X86_64'} | Select-Object -First 1
    if (-not $editor.url) { throw 'Official editor release not found' }
    $entry | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $downloadRoot 'release.json') -Encoding UTF8
    $hubPath = Join-Path $downloadRoot 'UnityHubSetup.exe'
    Write-InstallStatus 'hub-download' 'Downloading official Unity Hub'
    Get-OfficialFile 'https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe' $hubPath
    Assert-UnitySignature $hubPath
    $hubManifest = (Invoke-WebRequest -UseBasicParsing -Uri 'https://public-cdn.cloud.unity3d.com/hub/prod/latest.yml').Content
    $expectedHash = [regex]::Match($hubManifest, '(?m)^sha512:\s*(\S+)').Groups[1].Value
    $sha512 = [Security.Cryptography.SHA512]::Create()
    $actualHash = [Convert]::ToBase64String($sha512.ComputeHash([IO.File]::ReadAllBytes($hubPath)))
    $sha512.Dispose()
    if (-not $expectedHash -or $actualHash -ne $expectedHash) { throw 'Unity Hub hash does not match official manifest' }
    $hubDirectory = Join-Path $InstallRoot 'Hub'
    if (-not (Test-Path -LiteralPath (Join-Path $hubDirectory 'Unity Hub.exe'))) {
        Write-InstallStatus 'hub-install' 'Installing Unity Hub on data drive'
        $process = Start-Process -FilePath $hubPath -ArgumentList @('/S',('/D=' + $hubDirectory)) -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw ('Unity Hub installer returned ' + $process.ExitCode) }
    }
    $editorPath = Join-Path $downloadRoot ('UnitySetup64-' + $version + '.exe')
    $editorDirectory = Join-Path $InstallRoot ('Editors\' + $version)
    Write-InstallStatus 'editor-download' 'Downloading official Unity editor (about 4 GB)'
    Get-OfficialFile $editor.url $editorPath
    Assert-UnitySignature $editorPath
    if (-not (Test-Path -LiteralPath (Join-Path $editorDirectory 'Editor\Unity.exe'))) {
        Write-InstallStatus 'editor-install' 'Installing Unity editor on data drive'
        $process = Start-Process -FilePath $editorPath -ArgumentList @('/S',('/D=' + $editorDirectory)) -WindowStyle Hidden -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw ('Unity editor installer returned ' + $process.ExitCode) }
    }
    $android = $editor.modules | Where-Object {$_.id -eq 'android'} | Select-Object -First 1
    $androidPath = Join-Path $downloadRoot ('UnitySetup-Android-' + $version + '.exe')
    Write-InstallStatus 'android-download' 'Downloading Android player support'
    Get-OfficialFile $android.url $androidPath
    Assert-UnitySignature $androidPath
    Write-InstallStatus 'android-install' 'Installing Android player support'
    $process = Start-Process -FilePath $androidPath -ArgumentList @('/S',('/D=' + $editorDirectory)) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw ('Android installer returned ' + $process.ExitCode) }
    Write-InstallStatus 'editor-ready' ('Editor installed: ' + (Join-Path $editorDirectory 'Editor\Unity.exe'))
} catch {
    Write-InstallStatus 'error' $_.Exception.Message
    exit 1
}
