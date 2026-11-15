param(
    [Parameter(Mandatory=$true)][string]$TerrariaPath,
    [switch]$Tests
)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler was not found.' }
$core = Join-Path $TerrariaPath 'TerrariaModder\core\TerrariaModder.Core.dll'
$harmony = Join-Path $TerrariaPath 'TerrariaModder\core\deps\0Harmony.dll'
$game = Join-Path $TerrariaPath 'Terraria.exe'
foreach ($referencePath in @($core,$harmony,$game)) { if (-not (Test-Path -LiteralPath $referencePath)) { throw "Missing reference: $referencePath" } }
$buildDir = Join-Path $projectRoot 'build'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -ErrorAction SilentlyContinue | ForEach-Object {$_.FullName})
$references = @("/r:$core", "/r:$harmony", "/r:$game")
foreach ($xnaName in @('Microsoft.Xna.Framework','Microsoft.Xna.Framework.Game','Microsoft.Xna.Framework.Graphics')) {
    $xnaRoot = Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_32\$xnaName"
    $xnaFile = Get-ChildItem -LiteralPath $xnaRoot -Filter "$xnaName.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $xnaFile) { throw "Missing installed XNA runtime assembly: $xnaName" }
    $references += "/r:$($xnaFile.FullName)"
}
if ($Tests) {
    $sources += Join-Path $projectRoot 'tests\Tests.cs'
    & $compiler /nologo /utf8output /codepage:65001 /langversion:5 /optimize+ /platform:x86 /target:exe "/out:$buildDir\ShiftBatch.Tests.exe" $references $sources
} else {
    if ($sources.Count -eq 0) { throw 'No mod source files found.' }
    & $compiler /nologo /utf8output /codepage:65001 /langversion:5 /optimize+ /target:library "/out:$buildDir\ShiftBatch.dll" $references $sources
}
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed ($LASTEXITCODE)." }
