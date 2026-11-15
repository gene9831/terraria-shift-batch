param([string]$TerrariaPath = 'D:\SteamLibrary\steamapps\common\Terraria')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $projectRoot 'build\package-check'
$stagedMod = Join-Path $projectRoot 'build\package\TerrariaModder\mods\shift-batch'
New-Item -ItemType Directory -Path $stagedMod -Force | Out-Null
# A previous build must not leak README files or unexpected binaries into the ZIP.
foreach ($name in @('README.md','README.zh-CN.md','stale.dll')) {
    Set-Content -LiteralPath (Join-Path $stagedMod $name) -Value 'stale packaging fixture' -Encoding utf8
}
& (Join-Path $projectRoot 'package.ps1') -TerrariaPath $TerrariaPath -OutputDirectory $outputDir
$manifest = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'manifest.json') | ConvertFrom-Json
Add-Type -AssemblyName System.IO.Compression.FileSystem
$install = Join-Path $outputDir "ShiftBatch-$($manifest.version).zip"
$zip = [IO.Compression.ZipFile]::OpenRead($install)
try {
    $files = @($zip.Entries | Where-Object {$_.Name} | ForEach-Object {$_.FullName.Replace('\','/')})
    $expected = @('LICENSE.txt','manifest.json','ShiftBatch.dll') | ForEach-Object {"TerrariaModder/mods/shift-batch/$_"}
    if ((($files | Sort-Object) -join '|') -cne (($expected | Sort-Object) -join '|')) {
        throw "Runtime ZIP must contain exactly DLL, manifest and LICENSE; actual: $($files -join ', ')"
    }
} finally {$zip.Dispose()}
$version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $projectRoot 'build\ShiftBatch.dll')).Version.ToString()
if ($version -ne "$($manifest.version).0") {throw "DLL version does not match manifest: $version"}
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $outputDir "ShiftBatch-$($manifest.version)-source.zip"))
try {
    foreach ($name in @('README.md','README.zh-CN.md','ci/Validate-Release.ps1','.github/workflows/release.yml')) {
        if (-not ($zip.Entries | Where-Object {$_.FullName.Replace('\','/') -eq $name})) {
            throw "Source package lost $name"
        }
    }
} finally {$zip.Dispose()}
# Verify cleanup refuses a build junction into another directory. Both target
# and sentinel belong to this isolated fixture, never to a user's files.
$fixture = Join-Path $projectRoot ('build\package-safety-' + [Guid]::NewGuid().ToString('N'))
$fixtureProject = Join-Path $fixture 'project'
$outside = Join-Path $fixture 'outside'
New-Item -ItemType Directory -Path $fixtureProject,(Join-Path $outside 'package') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'package.ps1') -Destination $fixtureProject
foreach ($name in @('test.ps1','build.ps1')) {
    Set-Content -LiteralPath (Join-Path $fixtureProject $name) -Value '# No-op fixture; cleanup must fail before builds.'
}
Set-Content -LiteralPath (Join-Path $fixtureProject 'manifest.json') -Value '{"version":"1.0.0"}'
$sentinel = Join-Path $outside 'package\must-not-delete.txt'
Set-Content -LiteralPath $sentinel -Value 'owned safety fixture'
$junction = Join-Path $fixtureProject 'build'
New-Item -ItemType Junction -Path $junction -Target $outside | Out-Null
try {
    $rejection = ''
    try {& (Join-Path $fixtureProject 'package.ps1') -TerrariaPath $TerrariaPath -OutputDirectory (Join-Path $fixture 'output') | Out-Null}
    catch {$rejection = $_.Exception.Message}
    if ($rejection -notmatch 'reparse point' -or -not (Test-Path -LiteralPath $sentinel)) {
        throw 'Packaging must reject a build reparse point before deleting external staging contents.'
    }
} finally {
    # Non-recursive junction removal leaves its target intact.
    [IO.Directory]::Delete($junction)
}
Write-Output 'PASS: exactly 3 runtime files; no stale README/binary; source/CI files retained; versions match; junction cleanup rejected.'
