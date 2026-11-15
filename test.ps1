param([Parameter(Mandatory=$true)][string]$TerrariaPath)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -TerrariaPath $TerrariaPath -Tests
& (Join-Path $PSScriptRoot 'build\ShiftBatch.Tests.exe') $TerrariaPath
if ($LASTEXITCODE -ne 0) { throw "Tests failed ($LASTEXITCODE)." }
