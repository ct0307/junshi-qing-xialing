param([string]$Compiler = "C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe")
$ErrorActionPreference = "Stop"
$domainRoot = Join-Path $PSScriptRoot "../Assets/Scripts/Domain"
$outputRoot = Join-Path $PSScriptRoot "bin"
[void][System.IO.Directory]::CreateDirectory($outputRoot)
$sources = @(Get-ChildItem -LiteralPath $domainRoot -Filter *.cs | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot "CombatDiagnosticsTests.cs"
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$outputRoot/CombatDiagnosticsTests.exe" $sources
if ($LASTEXITCODE -ne 0) { throw "诊断测试编译失败" }
& (Join-Path $outputRoot "CombatDiagnosticsTests.exe") (Join-Path $PSScriptRoot "../Assets/Resources/Game/database.json")
if ($LASTEXITCODE -ne 0) { throw "诊断测试失败" }
