param(
    [string]$TerrariaPath = 'D:\SteamLibrary\steamapps\common\Terraria',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'test.ps1') -TerrariaPath $TerrariaPath
& (Join-Path $PSScriptRoot 'build.ps1') -TerrariaPath $TerrariaPath
$stagingRoot = Join-Path $PSScriptRoot 'build\package'
$modDir = Join-Path $stagingRoot 'TerrariaModder\mods\shift-batch'
New-Item -ItemType Directory -Path $modDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'build\ShiftBatch.dll') -Destination $modDir -Force
foreach ($name in @('manifest.json','README.md','README.zh-CN.md','LICENSE.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $modDir -Force
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$installZip = Join-Path $OutputDirectory 'ShiftBatch-1.1.0.zip'
Compress-Archive -LiteralPath (Join-Path $stagingRoot 'TerrariaModder') -DestinationPath $installZip -Force
$sourceFiles = @('src','tests','docs','build.ps1','test.ps1','package.ps1','ShiftBatch.csproj','manifest.json','README.md','README.zh-CN.md','LICENSE.txt','CHANGELOG.md','CONTRIBUTING.md','.gitignore','.gitattributes') | ForEach-Object { Join-Path $PSScriptRoot $_ }
Compress-Archive -LiteralPath $sourceFiles -DestinationPath (Join-Path $OutputDirectory 'ShiftBatch-1.1.0-source.zip') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.zh-CN.md') -Destination (Join-Path $OutputDirectory 'ShiftBatch-使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $OutputDirectory 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.zh-CN.md') -Destination (Join-Path $OutputDirectory 'README.zh-CN.md') -Force
Get-Item -LiteralPath $installZip,(Join-Path $OutputDirectory 'ShiftBatch-1.1.0-source.zip') | Select-Object Name,Length
