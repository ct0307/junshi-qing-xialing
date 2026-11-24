param(
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Revision = 'v01',
    [string]$UnityEditor = 'F:/Development/Unity/Editors/6000.3.28f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$artRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artProject = Join-Path $artRoot '.tools/ArtLab-v5-20261108'
$artOutput = Join-Path $artRoot ('docs/ui/art-refinement-v5/' + $Revision)
if (Test-Path -LiteralPath $artOutput) { throw '样稿版本目录已存在，请使用新版本名。' }
if ($env:UNITY_EDITOR_PATH) { $UnityEditor = $env:UNITY_EDITOR_PATH }
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw '未找到现有Unity编辑器。' }
if (-not (Test-Path -LiteralPath $artProject)) {
    [void][IO.Directory]::CreateDirectory($artProject)
    foreach ($artDirectory in @('Packages','ProjectSettings')) {
        Copy-Item -LiteralPath (Join-Path (Join-Path $artRoot 'UnityGame') $artDirectory) -Destination $artProject -Recurse
    }
}
$artEditor = Join-Path $artProject 'Assets/Editor'
[void][IO.Directory]::CreateDirectory($artEditor)
$artSourceRecords = @()
foreach ($artFile in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'artlab') -Filter '*.cs' -File) {
    $artCachedSource = Join-Path $artEditor $artFile.Name
    Copy-Item -LiteralPath $artFile.FullName -Destination $artCachedSource -Force
    $artSourceHash = (Get-FileHash -LiteralPath $artFile.FullName).Hash.ToLowerInvariant()
    if ((Get-FileHash -LiteralPath $artCachedSource).Hash.ToLowerInvariant() -ne $artSourceHash) { throw '渲染脚本复制指纹不一致。' }
    $artSourceRecords += [ordered]@{Path=$artFile.FullName; Sha256=$artSourceHash}
}
$artLog = Join-Path $artRoot ('.tools/artlab-' + $Revision + '.log')
$artStart = New-Object Diagnostics.ProcessStartInfo
$artStart.FileName = $UnityEditor
$artStart.Arguments = '-batchmode -quit -projectPath "' + $artProject + '" -executeMethod MiniatureRender.Run -logFile "' + $artLog + '"'
$artStart.UseShellExecute = $false
$artStart.CreateNoWindow = $true
$artStart.EnvironmentVariables['ARTLAB_OUTPUT'] = $artOutput
$artStart.EnvironmentVariables['ARTLAB_DATABASE'] = Join-Path $artRoot 'UnityGame/Assets/Resources/Game/database.json'
if ([string]::IsNullOrWhiteSpace($artStart.EnvironmentVariables['ALLUSERSPROFILE'])) {
    $artStart.EnvironmentVariables['ALLUSERSPROFILE'] = [Environment]::GetFolderPath('CommonApplicationData')
}
$artProcess = [Diagnostics.Process]::Start($artStart)
$artProcess.WaitForExit()
Get-Content -LiteralPath $artLog -Encoding UTF8 -Tail 40
if ($artProcess.ExitCode -ne 0) { throw ('美术样稿渲染失败，退出' + $artProcess.ExitCode + '；日志：' + $artLog) }
Copy-Item -LiteralPath $artLog -Destination (Join-Path $artOutput 'render.log')
[ordered]@{
    Revision=$Revision; UnityVersion='6000.3.28f1'; RenderExitCode=$artProcess.ExitCode
    VerifiedAtUtc=[DateTime]::UtcNow.ToString('o'); Scripts=$artSourceRecords
    DatabaseSha256=(Get-FileHash -LiteralPath $artStart.EnvironmentVariables['ARTLAB_DATABASE']).Hash.ToLowerInvariant()
    LogSha256=(Get-FileHash -LiteralPath $artLog).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artOutput 'render-manifest.json') -Encoding UTF8
Write-Output ('美术样稿渲染完成：' + $artOutput)
