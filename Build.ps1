param(
    [string]$Toolbox = $null
)

$ErrorActionPreference = "Stop"

$ProjectDir = $PSScriptRoot

$ProjectFile = Join-Path $ProjectDir "GameUpdateStatus.csproj"

if (-not $Toolbox) {
    $Toolbox = Join-Path $env:LOCALAPPDATA "Playnite\Toolbox.exe"
}
if (-not (Test-Path $Toolbox)) {
    throw "Playnite Toolbox introuvable : $Toolbox"
}

$BinDir = Join-Path $ProjectDir "bin"
$ReleaseDir = Join-Path $BinDir "Release"
$ReleaseDirNet = Join-Path $ReleaseDir "net462"

$ThemeDir = Join-Path $ProjectDir "Playnite\Themes\Desktop\Game Update Status"


# ----------------------------------------------------------------------
# Vérifications
# ----------------------------------------------------------------------

if (-not (Test-Path $Toolbox)) {
    throw "Playnite Toolbox introuvable : $Toolbox"
}

if (-not (Test-Path $ProjectFile)) {
    throw "Projet introuvable : $ProjectFile"
}

if (-not (Test-Path (Join-Path $ProjectDir "extension.toml"))) {
    throw "extension.toml introuvable."
}

if (-not (Test-Path $ThemeDir)) {
    throw "Dossier du thème introuvable : $ThemeDir"
}


# ----------------------------------------------------------------------
# Nettoyage
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Nettoyage ==="

Remove-Item $ReleaseDir -Recurse -Force -ErrorAction SilentlyContinue



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
# Packaging .pext
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Packaging .pext ==="

& $Toolbox pack `
    $ReleaseDirNet `
    (Join-Path $ReleaseDir "GameUpdateStatus.pext")

if ($LASTEXITCODE -ne 0) {
    throw "Le packaging de l'extension a échoué."
}




# ----------------------------------------------------------------------
# Packaging .pthm
# ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== Packaging .pthm ==="

Write-Host "Packaging du thème : $ThemeDir"

& $Toolbox pack `
    $ThemeDir `
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