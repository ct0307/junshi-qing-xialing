param([string]$Compiler = "C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")
$ErrorActionPreference = "Stop"
$domainRoot = Join-Path $PSScriptRoot "../Assets/Scripts/Domain"
$outputRoot = Join-Path $PSScriptRoot "bin"
[void][System.IO.Directory]::CreateDirectory($outputRoot)
$sources = @(Get-ChildItem -LiteralPath $domainRoot -Filter *.cs | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot "FactionTests.cs"
$sources += Join-Path $PSScriptRoot "FactionSimulationTests.cs"
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$outputRoot/FactionTests.exe" $sources
if ($LASTEXITCODE -ne 0) { throw "Faction test compilation failed." }
& (Join-Path $outputRoot "FactionTests.exe") (Join-Path $PSScriptRoot "../Assets/Resources/Game/database.json")
if ($LASTEXITCODE -ne 0) { throw "Faction tests failed." }
