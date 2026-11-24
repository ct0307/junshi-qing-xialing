param([string]$Compiler = "C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe", [switch]$Simulate)
$ErrorActionPreference = "Stop"
$testRoot = $PSScriptRoot
$domainRoot = Join-Path $testRoot "../Assets/Scripts/Domain"
$outputRoot = Join-Path $testRoot "bin"
if (-not (Test-Path -LiteralPath $Compiler)) { throw "找不到 C# 编译器：$Compiler" }
[void][System.IO.Directory]::CreateDirectory($outputRoot)
$sources = @(Get-ChildItem -LiteralPath $domainRoot -Filter *.cs | ForEach-Object { $_.FullName })
$sources += Join-Path $testRoot "DomainTests.cs"
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$outputRoot/DomainTests.exe" $sources
if ($LASTEXITCODE -ne 0) { throw "规则测试编译失败。" }
$testArguments = @((Join-Path $testRoot "../Assets/Resources/Game/database.json"))
if ($Simulate) { $testArguments += "--simulate" }
& (Join-Path $outputRoot "DomainTests.exe") $testArguments
if ($LASTEXITCODE -ne 0) { throw "规则测试失败。" }
