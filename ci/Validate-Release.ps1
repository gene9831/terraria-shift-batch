param(
    [Parameter(Mandatory=$true)][string]$TagName,
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
if ($TagName -notmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw 'Release tags must use vMAJOR.MINOR.PATCH, for example v1.0.0.'
}
$version = $TagName.Substring(1)
$manifest = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'manifest.json') | ConvertFrom-Json
if ($manifest.version -cne $version) {throw "Tag $TagName does not match manifest version $($manifest.version)."}
$source = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'src\AssemblyInfo.cs')
foreach ($attribute in @('AssemblyVersion','AssemblyFileVersion')) {
    $pattern = '\b' + $attribute + '\("([^"\r\n]+)"\)'
    if ($source -notmatch $pattern -or $Matches[1] -cne "$version.0") {
        throw "$attribute must match the tag: $version.0"
    }
}
Write-Output "Release version validated: $TagName, manifest $version, DLL $version.0"
