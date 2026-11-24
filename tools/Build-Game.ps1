param(
    [ValidateSet('Verify','Ui','Jade','Harmony','Header','App','Experience','FactionArmy','WindowsFactionArmy','AndroidFactionArmy','Race','WindowsRace','AndroidRace','Sensation','WindowsSensation','AndroidSensation','Audio','AudioPlayback','AudioRegressions','Android','Windows','WindowsJade','WindowsExperience')][string]$Target = 'Verify',
    [string]$UnityEditor = 'F:\Development\Unity\Editors\6000.3.28f1\Editor\Unity.exe',
    [string]$ProjectDirectory = ''
)
$ErrorActionPreference = 'Stop'
if ($env:UNITY_EDITOR_PATH) { $UnityEditor = $env:UNITY_EDITOR_PATH }
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw ('Unity editor not found: ' + $UnityEditor) }
if (-not $ProjectDirectory) { $ProjectDirectory = Join-Path $PSScriptRoot '..\UnityGame' }
$projectRoot = [IO.Path]::GetFullPath($ProjectDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Assets'))) { throw ('Unity Assets not found: ' + $projectRoot) }
if ($Target -in @('Android','AndroidFactionArmy','AndroidRace','AndroidSensation') -and $projectRoot -match '[^\x00-\x7F]') {
    throw 'Android构建要求纯英文工程路径；请将Assets、Packages、ProjectSettings复制到F盘英文目录，再使用-ProjectDirectory指定。'
}
$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$method = @{Verify='YanyiBuild.VerifyProject';Ui='UiBattleRenderChecks.Run';Jade='UiBattleRenderChecks.RunJade';Harmony='UiBattleRenderChecks.RunHarmony';Header='UiBattleRenderChecks.RunHeader';App='UiBattleRenderChecks.RunApp';Experience='UiBattleRenderChecks.RunExperience';Audio='UiAudioChecks.RunFromCommandLine';AudioPlayback='AudioPlaybackChecks.RunFromCommandLine';AudioRegressions='AudioPlaybackChecks.RunRegressionsFromCommandLine';Android='YanyiBuild.BuildAndroid';Windows='YanyiBuild.BuildWindows';WindowsJade='YanyiBuild.BuildWindowsJade';WindowsExperience='YanyiBuild.BuildWindowsExperience'}[$Target]
$logFile = Join-Path $logDirectory ('build-' + $Target.ToLowerInvariant() + '.log')
if ($Target -eq 'FactionArmy') { $method = 'UiFactionArmyChecks.Run' }
if ($Target -eq 'WindowsFactionArmy') { $method = 'YanyiBuild.BuildWindowsFactionArmy' }
if ($Target -eq 'AndroidFactionArmy') { $method = 'YanyiBuild.BuildAndroidFactionArmy' }
if ($Target -eq 'Race') { $method = 'UiRaceChecks.Run' }
if ($Target -eq 'WindowsRace') { $method = 'YanyiBuild.BuildWindowsRace' }
if ($Target -eq 'AndroidRace') { $method = 'YanyiBuild.BuildAndroidRace' }
if ($Target -eq 'Sensation') { $method = 'UiSensationChecks.Run' }
if ($Target -eq 'WindowsSensation') { $method = 'YanyiBuild.BuildWindowsSensation' }
if ($Target -eq 'AndroidSensation') { $method = 'YanyiBuild.BuildAndroidSensation' }
$arguments = @('-batchmode','-projectPath',('"' + $projectRoot + '"'),'-executeMethod',$method,'-logFile',('"' + $logFile + '"'))
$playback = $Target -eq 'AudioPlayback' -or $Target -eq 'AudioRegressions'
if ($playback) { $arguments += '-force-audio' } else { $arguments += '-quit' }
if ($Target -notin @('Ui','Jade','Harmony','Header','App','Experience','FactionArmy','Race','Sensation') -and -not $playback) { $arguments += '-nographics' }
if ($Target -in @('Android','AndroidFactionArmy','AndroidRace','AndroidSensation')) { $arguments += @('-buildTarget','Android') }
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $UnityEditor
$startInfo.Arguments = $arguments -join ' '
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
# 部分命令环境省略此变量，Unity 6.3包管理器会因此收到未定义路径；仅补齐子进程环境。
if ([string]::IsNullOrWhiteSpace($startInfo.EnvironmentVariables['ALLUSERSPROFILE'])) {
    $startInfo.EnvironmentVariables['ALLUSERSPROFILE'] = [Environment]::GetFolderPath('CommonApplicationData')
}
$process = [System.Diagnostics.Process]::Start($startInfo)
$process.WaitForExit()
Get-Content -LiteralPath $logFile -Tail 45 -Encoding UTF8
if ($process.ExitCode -ne 0) { throw ('Unity process returned ' + $process.ExitCode + '. Log: ' + $logFile) }
Write-Output ('Unity task finished: ' + $Target)
