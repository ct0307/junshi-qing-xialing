param([string]$Compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
$ErrorActionPreference = 'Stop'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../Assets/Scripts/Domain') -Filter *.cs | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot 'SensationExperimentTests.cs'
$binary = Join-Path $PSScriptRoot 'bin/SensationExperimentTests.exe'
[void][IO.Directory]::CreateDirectory((Split-Path -Parent $binary))
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$binary" $sources
if ($LASTEXITCODE -ne 0) { throw '体验实验编译失败。' }
$work = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../.planning/combat-sensation-implementation-20261119'))
& $binary (Join-Path $work 'candidates') (Join-Path $work 'experiments.json')
if ($LASTEXITCODE -ne 0) { throw '体验实验未达到公共节点与守恒要求。' }
