
#requires -Version 5.1

<#
    Prototype de détection du statut de mise à jour
    Steam + Epic Games

    Valeurs retournées :
      UP_TO_DATE
      UPDATE_AVAILABLE
      UNKNOWN

    Ce script ne modifie aucun fichier et ne lance aucun téléchargement.
#>

$ErrorActionPreference = "SilentlyContinue"

# ============================================================
# Steam
# ============================================================

function Parse-SteamVdf {
    param(
        [string]$Path
    )

    $result = @{}

    if (-not (Test-Path $Path)) {
        return $result
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^\s*"([^"]+)"\s*"([^"]*)"\s*$') {
            $result[$matches[1].ToLowerInvariant()] = $matches[2]
        }
    }

    return $result
}

function Get-SteamStatus {
    param(
        [string]$ManifestPath
    )

    $data = Parse-SteamVdf $ManifestPath

    if ($data.Count -eq 0) {
        return [PSCustomObject]@{
            Status = "UNKNOWN"
            Reason = "Manifest illisible"
        }
    }

    $name = $data["name"]
    $stateFlags = 0
    $bytesToDownload = 0
    $bytesDownloaded = 0
    $updateResult = 0

    [int64]::TryParse($data["stateflags"], [ref]$stateFlags) | Out-Null
    [int64]::TryParse($data["bytestodownload"], [ref]$bytesToDownload) | Out-Null
    [int64]::TryParse($data["bytesdownloaded"], [ref]$bytesDownloaded) | Out-Null
    [int64]::TryParse($data["updateresult"], [ref]$updateResult) | Out-Null

    # StateFlags 1 = jeu désinstallé
    if (($stateFlags -band 1) -ne 0) {
        return [PSCustomObject]@{
            Status = "UNKNOWN"
            Reason = "Jeu non installé"
        }
    }

    # Steam connaît explicitement des données à télécharger.
    if ($bytesToDownload -gt 0) {
        return [PSCustomObject]@{
            Status = "UPDATE_AVAILABLE"
            Reason = "BytesToDownload = $bytesToDownload"
        }
    }

    # Téléchargement partiel/en cours
    if ($bytesDownloaded -gt 0 -and $bytesToDownload -gt $bytesDownloaded) {
        return [PSCustomObject]@{
            Status = "UPDATE_AVAILABLE"
            Reason = "Téléchargement incomplet"
        }
    }

    # StateFlags 4 = entièrement installé.
    if (($stateFlags -band 4) -ne 0 -and $updateResult -eq 0) {
        return [PSCustomObject]@{
            Status = "UP_TO_DATE"
            Reason = "Steam indique que le jeu est installé et sans erreur"
        }
    }

    return [PSCustomObject]@{
        Status = "UNKNOWN"
        Reason = "État Steam insuffisant pour conclure"
    }
}


function Find-SteamLibraries {

    $libraries = @()

    $steamPaths = @(
        "${env:ProgramFiles(x86)}\Steam",
        "${env:ProgramFiles}\Steam",
        "${env:LOCALAPPDATA}\Steam"
    )

    foreach ($steamPath in $steamPaths) {

        if (-not (Test-Path $steamPath)) {
            continue
        }

        $libraryFile = Join-Path $steamPath "steamapps\libraryfolders.vdf"

        if (Test-Path $libraryFile) {

            $text = Get-Content -LiteralPath $libraryFile -Raw

            # Les lignes "path" contiennent les bibliothèques Steam.
            foreach ($match in [regex]::Matches($text, '"path"\s*"([^"]+)"')) {

                $path = $match.Groups[1].Value.Replace("\\", "\")

                if (Test-Path (Join-Path $path "steamapps")) {
                    $libraries += (Join-Path $path "steamapps")
                }
            }

            # La bibliothèque Steam principale
            $main = Join-Path $steamPath "steamapps"

            if (Test-Path $main) {
                $libraries += $main
            }
        }
    }

    return $libraries | Select-Object -Unique
}


function Get-SteamGames {

    $libraries = Find-SteamLibraries

    foreach ($library in $libraries) {

        Get-ChildItem -LiteralPath $library -Filter "appmanifest_*.acf" -File |
            ForEach-Object {

                $data = Parse-SteamVdf $_.FullName

                if ($data.Count -eq 0) {
                    return
                }

                $status = Get-SteamStatus $_.FullName

                [PSCustomObject]@{
                    Launcher = "Steam"
                    AppId = $data["appid"]
                    Name = $data["name"]
                    Status = $status.Status
                    Reason = $status.Reason
                    Manifest = $_.FullName
                    BuildId = $data["buildid"]
                    LastUpdated = $data["lastupdated"]
                    BytesToDownload = $data["bytestodownload"]
                }
            }
    }
}


# ============================================================
# Epic Games
# ============================================================

function Find-EpicManifestDirectory {

    $paths = @(
        "$env:ProgramData\Epic\EpicGamesLauncher\Data\Manifests",
        "$env:ProgramData\EpicGamesLauncher\Data\Manifests"
    )

    foreach ($path in $paths) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}


function Get-EpicGames {

    $manifestDir = Find-EpicManifestDirectory

    if (-not $manifestDir) {
        return
    }

    Get-ChildItem -LiteralPath $manifestDir -Filter "*.item" -File |
        ForEach-Object {

            try {
                $json = Get-Content -LiteralPath $_.FullName -Raw |
                    ConvertFrom-Json

                if (-not $json.DisplayName) {
                    return
                }

                $status = "UNKNOWN"
                $reason = "Manifest local uniquement"

                # Epic expose parfois l'état d'installation/staging
                # dans le manifest.
                if ($json.bIsIncompleteInstall -eq $true) {
                    $status = "UPDATE_AVAILABLE"
                    $reason = "Installation incomplète"
                }
                elseif ($json.bNeedsValidation -eq $true) {
                    $status = "UPDATE_AVAILABLE"
                    $reason = "Validation requise"
                }

                [PSCustomObject]@{
                    Launcher = "Epic"
                    AppId = $json.AppName
                    Name = $json.DisplayName
                    Status = $status
                    Reason = $reason
                    Manifest = $_.FullName
                    BuildId = $json.BuildVersion
                    LastUpdated = $null
                    BytesToDownload = $null
                }
            }
            catch {
                # Manifest Epic invalide : on l'ignore.
            }
        }
}


# ============================================================
# Exécution
# ============================================================

Write-Host ""
Write-Host "=== GAME UPDATE STATUS ===" -ForegroundColor Cyan
Write-Host ""

$games = @()

$games += @(Get-SteamGames)
$games += @(Get-EpicGames)

if ($games.Count -eq 0) {
    Write-Host "Aucun jeu Steam/Epic détecté." -ForegroundColor Yellow
    exit
}

# Affichage lisible
$games |
    Sort-Object Launcher, Name |
    Select-Object Launcher, Name, AppId, Status, Reason, BuildId, BytesToDownload |
    Format-Table -AutoSize

Write-Host ""
Write-Host "Résumé :" -ForegroundColor Cyan

$games |
    Group-Object Status |
    Sort-Object Name |
    ForEach-Object {
        Write-Host ("  {0,-18} {1}" -f $_.Name, $_.Count)
    }

Write-Host ""
