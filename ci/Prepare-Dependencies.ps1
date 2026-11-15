param(
    [string]$CacheDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) '.ci-deps'),
    [switch]$InstallXna
)
$ErrorActionPreference = 'Stop'
$CacheDirectory = [IO.Path]::GetFullPath($CacheDirectory)
New-Item -ItemType Directory -Path $CacheDirectory -Force | Out-Null
function Get-VerifiedDownload([string]$Url,[string]$Name,[string]$Sha256) {
    $path = Join-Path $CacheDirectory $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host "Downloading pinned public dependency: $Name"
        Invoke-WebRequest -Uri $Url -OutFile $path -UseBasicParsing
    }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $Sha256) {throw "Dependency checksum mismatch: $Name"}
    return $path
}
$server = Get-VerifiedDownload 'https://terraria.org/api/download/pc-dedicated-server/terraria-server-1458.zip' 'terraria-server-1458.zip' 'f513a4ac9789d34af766291ae217c9cd7d9472e13782a0e2b17512f70d7a8334'
$core = Get-VerifiedDownload 'https://github.com/Inidar1/terraria-modder/releases/download/core-v0.4.2/core-0.4.2.zip' 'core-0.4.2.zip' '33ac576645b104419a379d61487485f926c3b80485f80c357694a980bdfededd'
$xnaRoot = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_32\Microsoft.Xna.Framework.Game'
if (-not (Test-Path -LiteralPath $xnaRoot)) {
    if (-not $InstallXna) {throw 'XNA runtime missing. Use -InstallXna only on the disposable Windows CI runner.'}
    $msi = Get-VerifiedDownload 'https://download.microsoft.com/download/a/c/2/ac2c903b-e6e8-42c2-9fd7-bebac362a930/xnafx40_redist.msi' 'xnafx40_redist.msi' '47260420773a20443fb1e38a89b7f39c5237a80a842de598e6c8f7f90a3bbd6d'
    $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList @('/i',('"'+$msi+'"'),'/qn','/norestart') -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -notin @(0,3010)) {throw "XNA runtime installation failed: $($process.ExitCode)"}
}
$gameRoot = Join-Path $CacheDirectory 'game'
Expand-Archive -LiteralPath $core -DestinationPath $gameRoot -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$rawServer = Join-Path $CacheDirectory 'TerrariaServer.exe'
$zip = [IO.Compression.ZipFile]::OpenRead($server)
try {
    $entry = $zip.GetEntry('1458/Windows/TerrariaServer.exe')
    if ($null -eq $entry) {throw 'Official server archive layout changed.'}
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$rawServer,$true)
} finally {$zip.Dispose()}
Add-Type -Path (Join-Path $gameRoot 'TerrariaModder\core\deps\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($rawServer)
try {
    if ($assembly.Name.Name -cne 'TerrariaServer' -or $assembly.Name.Version.ToString() -cne '1.4.5.8') {throw 'Unexpected official server identity.'}
    $main = $assembly.MainModule.Types | Where-Object FullName -eq 'Terraria.Main'
    if ($main.BaseType.FullName -cne 'Terraria.Server.Game') {throw 'Official server Main base type changed.'}
    $xna = $assembly.MainModule.AssemblyReferences | Where-Object Name -eq 'Microsoft.Xna.Framework.Game'
    if (@($xna).Count -ne 1) {throw 'Official server XNA reference changed.'}
    # CI-only client-compatible reference/headless test assembly. Never launch
    # its entry point or package it. Main's original server-only base class must
    # not leak into the client mod's compiled member references.
    $assembly.Name.Name = 'Terraria'
    $assembly.MainModule.Name = 'Terraria.exe'
    $main.BaseType = [Mono.Cecil.TypeReference]::new('Microsoft.Xna.Framework','Game',$assembly.MainModule,$xna)
    $assembly.Write((Join-Path $gameRoot 'Terraria.exe'))
} finally {$assembly.Dispose()}
Write-Host 'Pinned public dependencies prepared for client API compilation and isolated headless tests.'
Write-Output $gameRoot
