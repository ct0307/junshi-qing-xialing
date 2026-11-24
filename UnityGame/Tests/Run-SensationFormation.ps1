param([string]$Database = '', [string]$Compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
$ErrorActionPreference = 'Stop'
if (-not $Database) { $Database = Join-Path $PSScriptRoot '../../.planning/combat-sensation-implementation-20261119/candidates/MN.json' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '../Assets/Scripts/Domain') -Filter *.cs | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot 'SensationFormationTests.cs'
$binary = Join-Path $PSScriptRoot 'bin/SensationFormationTests.exe'
[void][IO.Directory]::CreateDirectory((Split-Path -Parent $binary))
& $Compiler /nologo /langversion:5 /warnaserror+ /target:exe /reference:System.Web.Extensions.dll "/out:$binary" $sources
if ($LASTEXITCODE -ne 0) { throw 'Formation compile failed.' }
& $binary $Database (Join-Path $PSScriptRoot '../../.planning/combat-sensation-implementation-20261119/formation-results.json')
if ($LASTEXITCODE -ne 0) { throw 'Formation simulation failed.' }
