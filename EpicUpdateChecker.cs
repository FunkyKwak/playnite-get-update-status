using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Playnite.SDK;

namespace GameUpdateStatus
{
    public class EpicUpdateChecker
    {
        private readonly ILogger logger;

        public EpicUpdateChecker(ILogger logger)
        {
            this.logger = logger;
        }

        public List<StatusEntry> Check()
        {
            var results = new List<StatusEntry>();

            string epicPath = FindEpicGames();

            if (string.IsNullOrWhiteSpace(epicPath))
            {
                logger.Info("Epic Games Store introuvable.");
                return results;
            }

            logger.Info("Epic : " + epicPath);

            string[] manifestFolders =
            {
                Path.Combine(epicPath, "Manifests"),
                Path.Combine(epicPath, "InstalledSaves")
            };

            string manifestsPath = manifestFolders
                .FirstOrDefault(Directory.Exists);

            if (string.IsNullOrWhiteSpace(manifestsPath))
            {
                logger.Info("Aucun dossier de manifest Epic trouvé.");
                return results;
            }

            string[] manifestFiles = Directory.GetFiles(
                manifestsPath,
                "*.item");

            logger.Info("Jeux Epic installés : " + manifestFiles.Length);

            foreach (string manifest in manifestFiles)
            {
                try
                {
                    results.Add(CheckManifest(manifest));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Impossible de lire un manifeste Epic : " + manifest);
                }
            }

            return results;
        }

        private string FindEpicGames()
        {
            string[] candidatePaths =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpicGamesLauncher", "Data")
            };

            foreach (string path in candidatePaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                    return path;
            }

            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher"))
                {
                    string path = key?.GetValue("AppDataPath") as string;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        string installPath = Path.GetDirectoryName(path);
                        if (!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath))
                            return installPath;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Erreur lors de la recherche d'Epic dans le registre.");
            }

            return null;
        }

        private StatusEntry CheckManifest(string manifest)
        {
            string content = File.ReadAllText(manifest);

            string appName = GetValue(content, "DisplayName") ?? GetValue(content, "AppName") ?? Path.GetFileNameWithoutExtension(manifest);
            string appId = GetValue(content, "CatalogItemId") ?? GetValue(content, "AppName") ?? Path.GetFileNameWithoutExtension(manifest);
            string catalogNamespace = GetValue(content, "CatalogNamespace") ?? "";

            var result = new StatusEntry
            {
                Source = "Epic",
                AppId = appId,
                Name = appName,
                LocalBuild = null,
                PublicBuild = null,
                Status = UpdateStatus.Unknown.ToString(),
                CheckedAt = DateTime.Now.ToString("o")
            };

            string localBuild = GetValue(content, "AppVersionString") ?? GetValue(content, "BuildLabel") ?? GetValue(content, "BuildVersion");
            if (!string.IsNullOrWhiteSpace(localBuild))
            {
                result.LocalBuild = localBuild;
                logger.Info("[" + appId + "] " + appName + " - Local : " + localBuild);
            }
            else
            {
                return result;
            }

            string publicBuild = GetEpicPublicBuildId(catalogNamespace, appId);
            result.PublicBuild = publicBuild;

            if (string.IsNullOrWhiteSpace(publicBuild))
            {
                result.Status = UpdateStatus.Unknown.ToString();
                logger.Info("  Public : ?");
                logger.Info("  Etat   : Inconnu");
            }
            else if (localBuild == publicBuild)
            {
                result.Status = UpdateStatus.UpToDate.ToString();
                logger.Info("  Public : " + publicBuild);
                logger.Info("  Etat   : À jour");
            }
            else
            {
                result.Status = UpdateStatus.UpdateAvailable.ToString();
                logger.Info("  Public : " + publicBuild);
                logger.Info("  Etat   : Mise à jour disponible");
            }

            return result;
        }

        private string GetEpicPublicBuildId(string catalogNamespace, string catalogItemId)
        {
            if (string.IsNullOrWhiteSpace(catalogNamespace) || string.IsNullOrWhiteSpace(catalogItemId))
                return null;

            try
            {
                string url =
                    "https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/" +
                    catalogNamespace +
                    "/item/" +
                    catalogItemId +
                    "?locale=fr-FR";

                var request = WebRequest.CreateHttp(url);
                request.Method = "GET";
                request.Timeout = 15000;
                request.Accept = "application/json";

                logger.Info("Epic public lookup: " + url);

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    return ExtractEpicBuildId(json);
                }
            }
            catch (Exception ex)
            {
                logger.Info("Impossible de récupérer la version publique Epic pour " + catalogItemId + " : " + ex.Message);
                return null;
            }
        }

        private string ExtractEpicBuildId(string json)
        {
            string[] keys = { "appVersionString", "versionString", "buildVersion", "buildId" };

            foreach (string key in keys)
            {
                Match match = Regex.Match(
                    json,
                    "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (match.Success)
                    return match.Groups[1].Value;
            }

            return null;
        }

        private string GetValue(string content, string key)
        {
            Match match = Regex.Match(
                content,
                "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
