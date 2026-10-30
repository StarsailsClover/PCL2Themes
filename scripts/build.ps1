param(
    [string]$Configuration = 'Release',
    [string]$PclAddonPack = 'C:\Users\Sails\Documents\Workspace\02-Research\PCL2-dlswap-REA-2026-10-24\tools\PclAddonPack\bin\Release\net48\PclAddonPack.exe',
    [string]$Version = '',
    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionFile = Join-Path $root 'VERSION'
    if (-not (Test-Path $versionFile)) { throw 'VERSION file is missing and -Version was not supplied' }
    $Version = (Get-Content $versionFile -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($Version)) { throw 'version must not be empty' }
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root 'dist' }

$project = Join-Path $root 'src\PCL2Themes\PCL2Themes.csproj'
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'addon build failed' }

$bin = Join-Path $root "src\PCL2Themes\bin\$Configuration\net48"
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$out = Join-Path $OutDir 'pcl2themes.swapaddon'

& $PclAddonPack --project $bin --id 'com.starsailsclover.pcl2themes' --name 'PCL2 Themes' `
    --version $Version --entry 'PCL2Themes.ThemesAddon' `
    --api '>=1.0 <2.0' --shapes 'injected,patched' --host-versions '>=2.99.0' `
    --permissions 'host.hooks.attach,host.settings.write,host.ui.pages' `
    --description 'Themes, unlock and live theme editor for the official PCL2 launcher.' --out $out
if ($LASTEXITCODE -ne 0) { throw 'packaging failed' }

# Verify the manifest actually carries the version and scope we intended (the previous
# releases were packaged with a stale default version because the script never passed it).
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($out)
try {
    $entry = $zip.GetEntry('manifest.json')
    $reader = New-Object System.IO.StreamReader($entry.Open())
    $manifest = $reader.ReadToEnd() | ConvertFrom-Json
    $reader.Close()
} finally {
    $zip.Dispose()
}

Write-Host ("manifest: id={0} version={1} api={2}" -f $manifest.id, $manifest.version, $manifest.apiVersion)
Write-Host ("          shapes={0} hostVersions={1} permissions={2}" -f `
    ($manifest.shapes -join ','), ($manifest.hostVersions -join ','), ($manifest.permissions -join ','))
if ($manifest.version -ne $Version) { throw "manifest version mismatch: $($manifest.version) != $Version" }

$hash = (Get-FileHash $out -Algorithm SHA256).Hash
Write-Host "packed: $out"
Write-Host "sha256: $hash"
