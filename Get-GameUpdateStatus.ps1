# ============================================
# Steam Update Status - V4
# ============================================

$CacheFile = Join-Path $PSScriptRoot "update-status.json"
$CacheDurationMinutes = 30

# ============================================
# Vérification du cache
# ============================================

if (Test-Path $CacheFile) {

    $Cache = Get-Item $CacheFile

    $AgeMinutes = ((Get-Date) - $Cache.LastWriteTime).TotalMinutes

    if ($AgeMinutes -lt $CacheDurationMinutes) {

        Write-Host "Cache valide ($([math]::Round($AgeMinutes, 1)) min)." -ForegroundColor Cyan
        Write-Host ""

        Get-Content $CacheFile

        exit
    }
}

# ============================================
# Recherche de Steam
# ============================================

$SteamPath = "C:\Program Files (x86)\Steam"

if (-not (Test-Path $SteamPath)) {

    $SteamPath = (Get-ItemProperty `
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" `
        -ErrorAction SilentlyContinue).InstallPath
}

if (-not $SteamPath -or -not (Test-Path $SteamPath)) {

    Write-Host "Steam introuvable." -ForegroundColor Red
    exit 1
}

Write-Host "Steam : $SteamPath"
Write-Host ""

# ============================================
# Recherche des bibliothèques
# ============================================

$LibraryFolders = @()

$MainSteamApps = Join-Path $SteamPath "steamapps"

if (Test-Path $MainSteamApps) {
    $LibraryFolders += $MainSteamApps
}

$LibraryFile = Join-Path $MainSteamApps "libraryfolders.vdf"

if (Test-Path $LibraryFile) {

    $Content = Get-Content $LibraryFile -Raw

    $Paths = [regex]::Matches(
        $Content,
        '"path"\s+"([^"]+)"'
    )

    foreach ($Match in $Paths) {

        $Path = $Match.Groups[1].Value.Replace("\\", "\")

        $SteamApps = Join-Path $Path "steamapps"

        if ((Test-Path $SteamApps) -and
            ($LibraryFolders -notcontains $SteamApps)) {

            $LibraryFolders += $SteamApps
        }
    }
}

Write-Host "Bibliothèques trouvées : $($LibraryFolders.Count)"
Write-Host ""

# ============================================
# Fonction : BuildId public
# ============================================

function Get-SteamPublicBuildId {

    param(
        [string]$AppId
    )

    try {

        $Url = "https://api.steamcmd.net/v1/info/$AppId"

        $Response = Invoke-RestMethod `
            -Uri $Url `
            -Method Get `
            -TimeoutSec 15

        $AppInfo = $Response.data.$AppId

        if ($AppInfo) {

            $BuildId = $AppInfo.depots.branches.public.buildid

            if ($BuildId) {
                return [string]$BuildId
            }
        }
    }
    catch {
        return $null
    }

    return $null
}

# ============================================
# Recherche des jeux installés
# ============================================

$Manifests = foreach ($Library in $LibraryFolders) {

    Get-ChildItem `
        -Path $Library `
        -Filter "appmanifest_*.acf" `
        -File `
        -ErrorAction SilentlyContinue
}

Write-Host "Jeux Steam installés : $($Manifests.Count)"
Write-Host ""

# ============================================
# Analyse
# ============================================

$Results = @()

foreach ($Manifest in $Manifests) {

    $AppId = $Manifest.BaseName -replace "appmanifest_", ""

    $Content = Get-Content $Manifest.FullName -Raw

    # Nom
    $NameMatch = [regex]::Match(
        $Content,
        '"name"\s+"([^"]+)"'
    )

    $Name = if ($NameMatch.Success) {
        $NameMatch.Groups[1].Value
    }
    else {
        "AppID $AppId"
    }

    # BuildId local
    $BuildMatch = [regex]::Match(
        $Content,
        '"buildid"\s+"([^"]+)"'
    )

    if (-not $BuildMatch.Success) {

        $Results += [PSCustomObject]@{
            AppId       = $AppId
            Name        = $Name
            LocalBuild  = $null
            PublicBuild = $null
            Status      = "UNKNOWN"
            CheckedAt   = (Get-Date).ToString("o")
        }

        continue
    }

    $LocalBuild = $BuildMatch.Groups[1].Value

    Write-Host "[$AppId] $Name"
    Write-Host "  Local  : $LocalBuild"

    # Build public
    $PublicBuild = Get-SteamPublicBuildId $AppId

    if (-not $PublicBuild) {

        Write-Host "  Public : ?"
        Write-Host "  Etat   : ⚪ Inconnu" -ForegroundColor DarkYellow
        Write-Host ""

        $Status = "UNKNOWN"
    }
    elseif ($LocalBuild -eq $PublicBuild) {

        Write-Host "  Public : $PublicBuild"
        Write-Host "  Etat   : 🟢 À jour" -ForegroundColor Green
        Write-Host ""

        $Status = "UP_TO_DATE"
    }
    else {

        Write-Host "  Public : $PublicBuild"
        Write-Host "  Etat   : 🟠 Mise à jour disponible" -ForegroundColor Yellow
        Write-Host ""

        $Status = "UPDATE_AVAILABLE"
    }

    $Results += [PSCustomObject]@{
        AppId       = $AppId
        Name        = $Name
        LocalBuild  = $LocalBuild
        PublicBuild = $PublicBuild
        Status      = $Status
        CheckedAt   = (Get-Date).ToString("o")
    }
}

# ============================================
# Création du cache
# ============================================

$Results |
    ConvertTo-Json -Depth 5 |
    Set-Content `
        -Path $CacheFile `
        -Encoding UTF8

# ============================================
# Résumé
# ============================================

Write-Host ""
Write-Host "============================================"
Write-Host "Résultats"
Write-Host "============================================"

$Results |
    Select-Object AppId, Name, Status |
    Format-Table -AutoSize

Write-Host ""
Write-Host "Cache : $CacheFile"