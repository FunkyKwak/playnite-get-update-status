$ErrorActionPreference = "Stop"

$ProjectDir = $PSScriptRoot

$ProjectFile = Join-Path $ProjectDir "GameUpdateStatus.csproj"
$Toolbox = Join-Path $env:LOCALAPPDATA "Playnite\Toolbox.exe"

$ResourcesDir = Join-Path $ProjectDir "resources"
$BinDir = Join-Path $ProjectDir "bin"
$ReleaseDir = Join-Path $BinDir "Release"
$PackageDir = Join-Path $BinDir "Package"

$ExtensionPackageDir = Join-Path $PackageDir "Extension"
$ThemePackageDir = Join-Path $PackageDir "Theme"

$ThemeSourceDir = Join-Path $ProjectDir "Playnite\Themes\Desktop\Game Update Status"


# ----------------------------------------------------------------------
# Vérifications
# ----------------------------------------------------------------------

if (-not (Test-Path $Toolbox)) {
    throw "Playnite Toolbox introuvable : $Toolbox"
}

if (-not (Test-Path $ProjectFile)) {
    throw "Projet introuvable : $ProjectFile"
}

if (-not (Test-Path (Join-Path $ProjectDir "extension.yaml.template"))) {
    throw "extension.yaml introuvable."
}

if (-not (Test-Path $ThemeSourceDir)) {
    throw "Dossier du thème introuvable : $ThemeSourceDir"
}


# ----------------------------------------------------------------------
# Version de l'addon
# ----------------------------------------------------------------------
$VersionFile = Join-Path $ProjectDir "VERSION"

if (-not (Test-Path $VersionFile)) {
    throw "Fichier VERSION introuvable : $VersionFile"
}

$Version = (Get-Content $VersionFile -Raw).Trim()

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version invalide dans VERSION : '$Version'"
}

Write-Host "Version : $Version"

# ----------------------------------------------------------------------
# Nettoyage
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Nettoyage ==="

Remove-Item $PackageDir -Recurse -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null
New-Item -ItemType Directory -Path $ExtensionPackageDir -Force | Out-Null
New-Item -ItemType Directory -Path $ThemePackageDir -Force | Out-Null

# On conserve bin\Release pour la sortie finale.
New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null

# Supprime uniquement les anciens packages.
Get-ChildItem $ReleaseDir -File |
    Where-Object {
        $_.Extension -in ".pext", ".pthm"
    } |
    Remove-Item -Force


# ----------------------------------------------------------------------
# Compilation
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Compilation ==="

dotnet build `
    $ProjectFile `
    /property:GenerateFullPaths=true `
    /p:Configuration=Release `
    /p:Platform=AnyCPU `
    /consoleloggerparameters:NoSummary

if ($LASTEXITCODE -ne 0) {
    throw "La compilation a échoué."
}


# ----------------------------------------------------------------------
# Préparation du package extension
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Préparation de l'extension ==="


$ExtensionManifest = Get-Content `
    (Join-Path $ProjectDir "extension.yaml.template") `
    -Raw
$ExtensionManifest = $ExtensionManifest.Replace('$Version', $Version)
$ExtensionManifest |
    Set-Content `
        (Join-Path $ExtensionPackageDir "extension.yaml") `
        -Encoding UTF8

$DllPath = Join-Path $ReleaseDir "net462\GameUpdateStatus.dll"
if (-not (Test-Path $DllPath)) {
    throw "DLL compilée introuvable : $DllPath"
}
Copy-Item `
    $DllPath `
    $ExtensionPackageDir `
    -Force

Copy-item `
    -Force `
    -Recurse `
    -Verbose $ResourcesDir `
    -Destination $ExtensionPackageDir



# ----------------------------------------------------------------------
# Packaging .pext
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Packaging .pext ==="

& $Toolbox pack `
    $ExtensionPackageDir `
    $ReleaseDir

if ($LASTEXITCODE -ne 0) {
    throw "Le packaging de l'extension a échoué."
}


# ----------------------------------------------------------------------
# Préparation du package thème
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Préparation du thème Desktop ==="

$ThemeStagingDir = Join-Path $ThemePackageDir "Game Update Status"

Copy-Item `
    $ThemeSourceDir `
    $ThemeStagingDir `
    -Recurse `
    -Force

$ThemeManifest = Get-Content `
    (Join-Path $ThemeSourceDir "theme.yaml.template") `
    -Raw
$ThemeManifest = $ThemeManifest.Replace('$Version', $Version)
$ThemeManifest |
    Set-Content `
        (Join-Path $ThemeStagingDir "theme.yaml") `
        -Encoding UTF8


# ----------------------------------------------------------------------
# Packaging .pthm
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Packaging .pthm ==="

& $Toolbox pack `
    $ThemeStagingDir `
    $ReleaseDir

if ($LASTEXITCODE -ne 0) {
    throw "Le packaging du thème a échoué."
}


# ----------------------------------------------------------------------
# Nettoyage du staging
# ----------------------------------------------------------------------

Remove-Item $PackageDir -Recurse -Force


# ----------------------------------------------------------------------
# Résultat
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "========================================"
Write-Host " BUILD TERMINE"
Write-Host "========================================"
Write-Host ""
Write-Host "Packages générés dans :"
Write-Host "  $ReleaseDir"
Write-Host ""

$Packages = Get-ChildItem $ReleaseDir -File |
    Where-Object {
        $_.Extension -in ".pext", ".pthm"
    }

if ($Packages.Count -eq 0) {
    throw "Aucun package n'a été généré."
}

foreach ($Package in $Packages) {
    Write-Host "  $($Package.Name)"
}

Write-Host ""