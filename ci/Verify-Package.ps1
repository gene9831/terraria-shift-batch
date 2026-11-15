param(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [switch]$WriteChecksums
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {throw 'Invalid release version.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Read-ZipText($Zip, [string]$Name) {
    $entry = $Zip.Entries | Where-Object {$_.FullName.Replace('\','/') -ceq $Name}
    if (@($entry).Count -ne 1) {throw "Missing or duplicated archive entry: $Name"}
    $reader = [IO.StreamReader]::new($entry.Open(),[Text.Encoding]::UTF8)
    try {return $reader.ReadToEnd()} finally {$reader.Dispose()}
}
$installName = "ShiftBatch-$Version.zip"
$sourceName = "ShiftBatch-$Version-source.zip"
$installPath = Join-Path $PackageDirectory $installName
$sourcePath = Join-Path $PackageDirectory $sourceName
$zip = [IO.Compression.ZipFile]::OpenRead($installPath)
try {
    $files = @($zip.Entries | Where-Object {$_.Name} | ForEach-Object {$_.FullName.Replace('\','/')} | Sort-Object)
    $prefix = 'TerrariaModder/mods/shift-batch/'
    $expected = @('LICENSE.txt','manifest.json','ShiftBatch.dll') | ForEach-Object {$prefix + $_} | Sort-Object
    if (($files -join '|') -cne ($expected -join '|')) {throw 'Runtime archive must contain only the mod DLL, manifest and LICENSE.'}
    $manifest = (Read-ZipText $zip ($prefix+'manifest.json')) | ConvertFrom-Json
    if ($manifest.id -cne 'shift-batch' -or $manifest.version -cne $Version -or $manifest.entry_dll -cne 'ShiftBatch.dll') {
        throw 'Runtime archive identity/version does not match the release.'
    }
    $temporary = Join-Path (Split-Path -Parent $PSScriptRoot) ('build\release-dll-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temporary -Force | Out-Null
    $dll = Join-Path $temporary 'ShiftBatch.dll'
    $entry = $zip.Entries | Where-Object {$_.FullName.Replace('\','/') -ceq ($prefix+'ShiftBatch.dll')}
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$dll)
    if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString() -cne "$Version.0" -or
        [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion -cne "$Version.0") {
        throw 'Packaged DLL assembly/file version does not match the release.'
    }
} finally {$zip.Dispose()}
$zip = [IO.Compression.ZipFile]::OpenRead($sourcePath)
try {
    $files = @($zip.Entries | Where-Object {$_.Name} | ForEach-Object {$_.FullName.Replace('\','/')})
    if (@($files | Select-Object -Unique).Count -ne $files.Count) {throw 'Source archive has duplicated file names.'}
    foreach ($name in $files) {
        if ($name -match '^/|^[A-Za-z]:|(^|/)\.\.(/|$)|(^|/)\.git(/|$)|(^|/)(build|dist|bin|obj)(/|$)|\.(dll|exe|pdb|log)$') {
            throw "Unexpected source archive file: $name"
        }
    }
    foreach ($required in @('README.md','README.zh-CN.md','src/BatchRules.cs')) {
        if ($files -cnotcontains $required) {throw "Source archive missing $required"}
    }
    $manifest = (Read-ZipText $zip 'manifest.json') | ConvertFrom-Json
    if ($manifest.id -cne 'shift-batch' -or $manifest.version -cne $Version) {throw 'Source manifest does not match the release.'}
} finally {$zip.Dispose()}
$sums = @($installName,$sourceName) | ForEach-Object {
    (Get-FileHash -LiteralPath (Join-Path $PackageDirectory $_) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_
}
$checksumPath = Join-Path $PackageDirectory 'SHA256SUMS.txt'
if ($WriteChecksums) {
    [IO.File]::WriteAllLines([IO.Path]::GetFullPath($checksumPath),$sums,[Text.UTF8Encoding]::new($false))
} else {
    $existing = @([IO.File]::ReadAllLines([IO.Path]::GetFullPath($checksumPath)) | Where-Object {$_.Trim()})
    if (($existing -join '|') -cne ($sums -join '|')) {throw 'Release checksums do not match the downloaded archives.'}
}
Write-Output "Release packages verified: $Version; runtime/source contents, DLL version and SHA256 checksums match."
