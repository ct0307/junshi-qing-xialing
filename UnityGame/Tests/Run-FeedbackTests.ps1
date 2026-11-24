param([string]$Compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
$ErrorActionPreference = 'Stop'
$outputRoot = Join-Path $PSScriptRoot 'bin'
[void][IO.Directory]::CreateDirectory($outputRoot)
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../Assets/Scripts/Domain') -Filter *.cs | ForEach-Object FullName)
$trackerPath = Join-Path $PSScriptRoot '../Assets/Scripts/Presentation/BattleFeedbackState.cs'
if (Test-Path -LiteralPath $trackerPath) { $sources += $trackerPath }
$sources += Join-Path $PSScriptRoot 'FeedbackStateTests.cs'
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe "/out:$outputRoot/FeedbackStateTests.exe" $sources
if ($LASTEXITCODE -ne 0) { throw '反馈状态测试编译失败' }
& (Join-Path $outputRoot 'FeedbackStateTests.exe')
if ($LASTEXITCODE -ne 0) { throw '反馈状态测试失败' }
