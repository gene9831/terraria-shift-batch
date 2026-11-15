param(
    [Parameter(Mandatory=$true)][string]$TerrariaPath,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)
$ErrorActionPreference = 'Stop'
$buildRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'build'))
$stagingRoot = [IO.Path]::GetFullPath((Join-Path $buildRoot 'package'))
# Resolve-Path is lexical and does not resolve junction targets. Reject links
# before running builds or recursively cleaning anything in the staging tree.
foreach ($path in @($PSScriptRoot,$buildRoot,$stagingRoot)) {
    if (Test-Path -LiteralPath $path) {
        $item = Get-Item -LiteralPath $path -Force
        if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing package path through a file or reparse point: $path"
        }
    }
}
if (Test-Path -LiteralPath $stagingRoot) {
    $directories = New-Object 'System.Collections.Generic.Stack[string]'
    $directories.Push($stagingRoot)
    while ($directories.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $directories.Pop() -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing staging cleanup through a reparse point: $($item.FullName)"
            }
            if ($item.PSIsContainer) {$directories.Push($item.FullName)}
        }
    }
}
& (Join-Path $PSScriptRoot 'test.ps1') -TerrariaPath $TerrariaPath
& (Join-Path $PSScriptRoot 'build.ps1') -TerrariaPath $TerrariaPath
$manifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') | ConvertFrom-Json
$version = $manifest.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Manifest must contain a valid three-part product version.' }
if (Test-Path -LiteralPath $stagingRoot) {
    $resolved = (Resolve-Path -LiteralPath $stagingRoot).Path
    if ($resolved -ne $stagingRoot -or -not $resolved.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to clean staging outside the project build directory.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
$modDir = Join-Path $stagingRoot 'TerrariaModder\mods\shift-batch'
New-Item -ItemType Directory -Path $modDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build\ShiftBatch.dll') -Destination $modDir -Force
foreach ($name in @('manifest.json','LICENSE.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $modDir -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$installZip = Join-Path $OutputDirectory "ShiftBatch-$version.zip"
Compress-Archive -LiteralPath (Join-Path $stagingRoot 'TerrariaModder') -DestinationPath $installZip -Force
$sourceFiles = @('src','tests','docs','ci','.github','build.ps1','test.ps1','package.ps1','ShiftBatch.csproj','manifest.json','README.md','README.zh-CN.md','LICENSE.txt','CHANGELOG.md','CONTRIBUTING.md','.gitignore','.gitattributes') | ForEach-Object { Join-Path $PSScriptRoot $_ }
Compress-Archive -LiteralPath $sourceFiles -DestinationPath (Join-Path $OutputDirectory "ShiftBatch-$version-source.zip") -Force
Get-Item -LiteralPath $installZip,(Join-Path $OutputDirectory "ShiftBatch-$version-source.zip") | Select-Object Name,Length
