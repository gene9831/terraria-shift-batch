param(
    [Parameter(Mandatory=$true)][string]$DllPath,
    [Parameter(Mandatory=$true)][string]$CecilPath
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $CecilPath
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($DllPath))
try {
    $game = @($assembly.MainModule.AssemblyReferences | Where-Object Name -eq 'Terraria')
    if ($game.Count -ne 1 -or $game[0].Version.ToString() -cne '1.4.5.8') {throw 'Mod must reference the supported client assembly.'}
    if ($assembly.MainModule.AssemblyReferences | Where-Object Name -eq 'TerrariaServer') {throw 'Mod references the dedicated server assembly.'}
    if ($assembly.MainModule.GetTypeReferences() | Where-Object FullName -like 'Terraria.Server.*') {throw 'Mod references server-only APIs.'}
} finally {$assembly.Dispose()}
Write-Output 'PASS: compiled mod references the client assembly and no server-only APIs.'
