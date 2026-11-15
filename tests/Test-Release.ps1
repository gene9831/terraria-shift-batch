param([string]$PackageDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'build\package-check'))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$validate = Join-Path $projectRoot 'ci\Validate-Release.ps1'
if (-not (Test-Path -LiteralPath $validate)) {throw 'Release tag validation is missing.'}
$fixture = Join-Path $projectRoot ('build\release-rules-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'src') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $fixture 'manifest.json') -Value '{"version":"1.0.0"}'
$assemblyInfo = '[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]'
Set-Content -LiteralPath (Join-Path $fixture 'src\AssemblyInfo.cs') -Value $assemblyInfo
& $validate -TagName 'v1.0.0' -ProjectRoot $fixture
foreach ($tag in @('1.0.0','v1.0','v2.0.0','v1.0.0-extra')) {
    $rejected = $false
    try {& $validate -TagName $tag -ProjectRoot $fixture | Out-Null} catch {$rejected = $true}
    if (-not $rejected) {throw "Invalid/mismatched tag was accepted: $tag"}
}
Set-Content -LiteralPath (Join-Path $fixture 'src\AssemblyInfo.cs') -Value $assemblyInfo.Replace('AssemblyVersion("1.0.0.0")','AssemblyVersion("1.0.1.0")')
$rejected = $false
try {& $validate -TagName 'v1.0.0' -ProjectRoot $fixture | Out-Null} catch {$rejected = $true}
if (-not $rejected) {throw 'Mismatched assembly version was accepted.'}
Set-Content -LiteralPath (Join-Path $fixture 'src\AssemblyInfo.cs') -Value $assemblyInfo.Replace('AssemblyFileVersion("1.0.0.0")','AssemblyFileVersion("1.0.1.0")')
$rejected = $false
try {& $validate -TagName 'v1.0.0' -ProjectRoot $fixture | Out-Null} catch {$rejected = $true}
if (-not $rejected) {throw 'Mismatched DLL file version was accepted.'}
Write-Output 'PASS: tag format, manifest version, assembly version and DLL file version are enforced.'
$verify = Join-Path $projectRoot 'ci\Verify-Package.ps1'
if (-not (Test-Path -LiteralPath $verify)) {throw 'Release artifact validation is missing.'}
$manifest = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'manifest.json') | ConvertFrom-Json
$version = $manifest.version
$packages = Join-Path $fixture 'packages'
New-Item -ItemType Directory -Path $packages | Out-Null
foreach ($name in @("ShiftBatch-$version.zip","ShiftBatch-$version-source.zip")) {
    Copy-Item -LiteralPath (Join-Path $PackageDirectory $name) -Destination $packages
}
& $verify -Version $version -PackageDirectory $packages -WriteChecksums
& $verify -Version $version -PackageDirectory $packages
$sums = Join-Path $packages 'SHA256SUMS.txt'
$correct = [IO.File]::ReadAllText($sums)
[IO.File]::WriteAllText($sums,$correct.Replace($correct.Substring(0,64),('0'*64)))
$rejected = $false
try {& $verify -Version $version -PackageDirectory $packages | Out-Null} catch {$rejected = $true}
if (-not $rejected) {throw 'A wrong release checksum was accepted.'}
[IO.File]::WriteAllText($sums,$correct)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open((Join-Path $packages "ShiftBatch-$version.zip"),[IO.Compression.ZipArchiveMode]::Update)
try {
    $entry=$zip.CreateEntry('TerrariaModder/mods/shift-batch/README.md')
    $writer=[IO.StreamWriter]::new($entry.Open()); $writer.Write('unexpected runtime file'); $writer.Dispose()
} finally {$zip.Dispose()}
$rejected = $false
try {& $verify -Version $version -PackageDirectory $packages -WriteChecksums | Out-Null} catch {$rejected = $true}
if (-not $rejected) {throw 'Unexpected runtime README was accepted.'}
Copy-Item -LiteralPath (Join-Path $PackageDirectory "ShiftBatch-$version.zip") -Destination $packages -Force
$zip = [IO.Compression.ZipFile]::Open((Join-Path $packages "ShiftBatch-$version-source.zip"),[IO.Compression.ZipArchiveMode]::Update)
try {
    $entry=$zip.CreateEntry('Terraria.exe')
    $writer=[IO.StreamWriter]::new($entry.Open()); $writer.Write('unexpected binary'); $writer.Dispose()
} finally {$zip.Dispose()}
$rejected = $false
try {& $verify -Version $version -PackageDirectory $packages -WriteChecksums | Out-Null} catch {$rejected = $true}
if (-not $rejected) {throw 'A game binary in the source archive was accepted.'}
Write-Output 'PASS: release ZIP versions/contents and checksums are enforced; unexpected files are rejected.'
