param([string]$Compiler = "C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")
$ErrorActionPreference = "Stop"
$outputRoot = Join-Path $PSScriptRoot "bin"
[void][System.IO.Directory]::CreateDirectory($outputRoot)
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "../Assets/Scripts/Domain") -Filter *.cs | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot "RaceDomainTests.cs"
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$outputRoot/RaceDomainTests.exe" $sources
if ($LASTEXITCODE -ne 0) { throw "竞速专项编译失败。" }
& (Join-Path $outputRoot "RaceDomainTests.exe")
if ($LASTEXITCODE -ne 0) { throw "竞速专项失败。" }

$recordSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "../Assets/Scripts/Domain") -Filter *.cs | ForEach-Object { $_.FullName })
$recordSources += Join-Path $PSScriptRoot "../Assets/Scripts/Presentation/FastestRecord.cs"
$recordSources += Join-Path $PSScriptRoot "../Assets/Scripts/Presentation/GameProgress.cs"
$recordSources += Join-Path $PSScriptRoot "../Assets/Scripts/Presentation/RaceRecord.cs"
$recordSources += Join-Path $PSScriptRoot "../Assets/Scripts/Presentation/RaceProgress.cs"
$recordSources += Join-Path $PSScriptRoot "../Assets/Scripts/Presentation/RaceRecordCodec.cs"
$recordSources += Join-Path $PSScriptRoot "RaceRecordTests.cs"
$recordSources += Join-Path $PSScriptRoot "../Assets/Editor/UiFastestRecordChecks.cs"
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$outputRoot/RaceRecordTests.exe" $recordSources
if ($LASTEXITCODE -ne 0) { throw "竞速存档专项编译失败。" }
& (Join-Path $outputRoot "RaceRecordTests.exe")
if ($LASTEXITCODE -ne 0) { throw "竞速存档专项失败。" }
