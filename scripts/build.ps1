param(
    [string]$Configuration = 'Release',
    [string]$PclAddonPack = 'C:\Users\Sails\Documents\Workspace\02-Research\PCL2-dlswap-REA-2026-10-24\tools\PclAddonPack\bin\Release\net48\PclAddonPack.exe',
    [string]$Version = 'v26.0-Alpha 1',
    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root 'dist' }

$project = Join-Path $root 'src\PCL2Themes\PCL2Themes.csproj'
dotnet build $project -c $Configuration
if ($LASTEXITCODE -ne 0) { throw 'addon build failed' }

$bin = Join-Path $root "src\PCL2Themes\bin\$Configuration\net48"
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$out = Join-Path $OutDir 'pcl2themes.swapaddon'

& $PclAddonPack --project $bin --id 'com.starsailsclover.pcl2themes' --name 'PCL2 Themes' `
    --version $Version --entry 'PCL2Themes.ThemesAddon' `
    --description 'Themes, unlock and live theme editor for the official PCL2 launcher.' --out $out
if ($LASTEXITCODE -ne 0) { throw 'packaging failed' }

$hash = (Get-FileHash $out -Algorithm SHA256).Hash
Write-Host "packed: $out"
Write-Host "sha256: $hash"
