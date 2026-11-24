param(
    [string]$Compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe',
    [string]$Output = ''
)
$ErrorActionPreference = 'Stop'
if (-not $Output) { $Output = Join-Path $PSScriptRoot 'bin/race-simulation-results.json' }
$simulationSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../Assets/Scripts/Domain') -Filter *.cs | ForEach-Object { $_.FullName })
$simulationSources += Join-Path $PSScriptRoot 'RaceSimulationTests.cs'
$simulationBin = Join-Path $PSScriptRoot 'bin/RaceSimulationTests.exe'
[void][IO.Directory]::CreateDirectory((Split-Path -Parent $simulationBin))
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$simulationBin" $simulationSources
if ($LASTEXITCODE -ne 0) { throw '竞速策略模拟编译失败。' }
& $simulationBin (Join-Path $PSScriptRoot '../Assets/Resources/Game/database.json') $Output
if ($LASTEXITCODE -ne 0) { throw '竞速策略模拟未达验收目标，请读取详细结果。' }
