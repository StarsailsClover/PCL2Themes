param(
    [string]$Configuration = 'Release',
    [string]$PclAddonPack = 'C:\Users\Sails\Documents\Workspace\02-Research\PCL2-dlswap-REA-2026-10-24\tools\PclAddonPack\bin\Release\net48\PclAddonPack.exe',
    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root 'dist' }

$project = Join-Path $root 'tools\PCL2Themes.Probe\PCL2Themes.Probe.csproj'
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'probe build failed' }

$bin = Join-Path $root "tools\PCL2Themes.Probe\bin\$Configuration\net48"
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$out = Join-Path $OutDir 'pcl2themes-probe.swapaddon'

& $PclAddonPack --project $bin --id 'com.starsailsclover.pcl2themes.probe' --name 'PCL2Themes Probe' `
    --version '0.1.0' --entry 'PCL2Themes.Probe.ProbeAddon' `
    --description 'Runtime probe for PCL2Themes host research.' --out $out
if ($LASTEXITCODE -ne 0) { throw 'probe packaging failed' }
Write-Host "packed: $out"
