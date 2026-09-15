# ============================================
# Steam Update Status - V3
# ============================================

$SteamPath = "C:\Program Files (x86)\Steam"

# Recherche du dossier Steam si le chemin standard n'existe pas
if (-not (Test-Path $SteamPath)) {
    $SteamPath = (Get-ItemProperty `
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" `
        -ErrorAction SilentlyContinue).InstallPath
}

if (-not $SteamPath -or -not (Test-Path $SteamPath)) {
    Write-Host "Steam introuvable." -ForegroundColor Red
    exit
}

Write-Host "Steam : $SteamPath"
Write-Host ""

# --------------------------------------------
# Recherche des bibliothèques Steam
# --------------------------------------------

$LibraryFolders = @()

$MainSteamApps = Join-Path $SteamPath "steamapps"

if (Test-Path $MainSteamApps) {
    $LibraryFolders += $MainSteamApps
}

$LibraryFile = Join-Path $MainSteamApps "libraryfolders.vdf"

if (Test-Path $LibraryFile) {

    $Content = Get-Content $LibraryFile -Raw

    # Extrait les chemins des bibliothèques
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

# --------------------------------------------
# Fonction : BuildId public Steam
# --------------------------------------------

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

        # Selon la structure de la réponse,
        # les données peuvent être sous data
        $AppInfo = $Response.data.$AppId

        if (-not $AppInfo) {
            return $null
        }

        $BuildId = $AppInfo.depots.branches.public.buildid

        if ($BuildId) {
            return [string]$BuildId
        }

    }
    catch {
        return $null
    }

    return $null
}

# --------------------------------------------
# Recherche des jeux installés
# --------------------------------------------

$Manifests = foreach ($Library in $LibraryFolders) {

    Get-ChildItem `
        -Path $Library `
        -Filter "appmanifest_*.acf" `
        -File `
        -ErrorAction SilentlyContinue
}

Write-Host "Jeux Steam installés trouvés : $($Manifests.Count)"
Write-Host ""

# --------------------------------------------
# Analyse
# --------------------------------------------

$Results = foreach ($Manifest in $Manifests) {

    $AppId = $Manifest.BaseName -replace "appmanifest_", ""

    $Content = Get-Content $Manifest.FullName -Raw

    # Nom du jeu
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

        [PSCustomObject]@{
            AppId       = $AppId
            Name        = $Name
            LocalBuild  = $null
            PublicBuild = $null
            Status      = "UNKNOWN"
            Reason      = "BuildId local introuvable"
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
        Write-Host "  Etat   : UNKNOWN" -ForegroundColor Yellow
        Write-Host ""

        [PSCustomObject]@{
            AppId       = $AppId
            Name        = $Name
            LocalBuild  = $LocalBuild
            PublicBuild = $null
            Status      = "UNKNOWN"
            Reason      = "Impossible de récupérer le build public"
        }

        continue
    }

    Write-Host "  Public : $PublicBuild"

    if ($LocalBuild -eq $PublicBuild) {

        $Status = "UP_TO_DATE"
        Write-Host "  Etat   : 🟢 À jour" -ForegroundColor Green

    }
    else {

        $Status = "UPDATE_AVAILABLE"
        Write-Host "  Etat   : 🟠 Mise à jour disponible" -ForegroundColor Yellow
    }

    Write-Host ""

    [PSCustomObject]@{
        AppId       = $AppId
        Name        = $Name
        LocalBuild  = $LocalBuild
        PublicBuild = $PublicBuild
        Status      = $Status
        Reason      = ""
    }
}

# --------------------------------------------
# Résumé
# --------------------------------------------

Write-Host ""
Write-Host "============================================"
Write-Host "Résumé"
Write-Host "============================================"

$Results |
    Select-Object AppId, Name, LocalBuild, PublicBuild, Status |
    Format-Table -AutoSize