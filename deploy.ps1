# Deploy BoplMoreColors to local Thunderstore profile for testing
param(
    [string]$BoplBattleRootDir = "C:\Program Files (x86)\Steam\steamapps\common\Bopl Battle",
    [string]$BoplProfileDir = "C:\Users\colli\AppData\Roaming\Thunderstore Mod Manager\DataFolder\BoplBattle\profiles\test my mods"
)

$ErrorActionPreference = 'Stop'

Write-Host "Building BoplMoreColors..." -ForegroundColor Cyan
dotnet build `
    -p:BoplBattleRootDir="$BoplBattleRootDir" `
    -p:BoplProfileDir="$BoplProfileDir"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

Write-Host "Build + deploy succeeded." -ForegroundColor Green
Write-Host "DLL deployed to: $BoplProfileDir\BepInEx\plugins\BoplMoreColors\BoplMoreColors.dll"
