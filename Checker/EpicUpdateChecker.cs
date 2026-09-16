using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Playnite.SDK;

namespace GameUpdateStatus
{
    public class EpicUpdateChecker : Checker
    {
        public EpicUpdateChecker(ILogger logger, IPlayniteAPI api) : base(logger)
        {
            _legendaryManager = new LegendaryManager(api.Dialogs);
        }
        private LegendaryManager _legendaryManager;
        public override async Task<List<StatusEntry>> Check()
        {
            bool isAuthenticated = await _legendaryManager.EnsureAuthenticatedAsync();
            if (isAuthenticated)
            {
                var results = new List<StatusEntry>();

                string epicPath = FindLauncherPath();

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
                        results.Add(await CheckSingle(manifest));
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Impossible de lire un manifeste Epic : " + manifest);
                    }
                }

                return results;
            }
            logger.Info("L'utilisateur n'est pas connecté à Epic via Legendary");
            return null;
        }

        protected override string FindLauncherPath()
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


        protected override StatusEntry GetLocalInfo(string manifest)
        {
            string content = File.ReadAllText(manifest);

            string appName = GetValue(content, "DisplayName") ?? GetValue(content, "AppName") ?? Path.GetFileNameWithoutExtension(manifest);
            string appId = GetValue(content, "AppName") ?? GetValue(content, "CatalogItemId") ?? Path.GetFileNameWithoutExtension(manifest);
            string localBuild = GetValue(content, "AppVersionString") ?? GetValue(content, "BuildLabel") ?? GetValue(content, "BuildVersion");

            var result = new StatusEntry
            {
                Source = "Epic",
                AppId = appId,
                Name = appName,
                LocalBuild = localBuild,
                PublicBuild = null,
                Status = UpdateStatus.Unknown.ToString(),
                CheckedAt = DateTime.Now.ToString("o")
            };

            logger.Info("[" + appId + "] " + appName + " - Local : " + localBuild);

            return result;
        }

        /// <summary>
        /// GetPublicBuildId
        /// </summary>
        /// <param name="key">Striniig containing catalogNamespace:appId:storeUrl</param>
        /// <returns></returns>
        protected override async Task<string> GetPublicBuildId(string appId)
        {
            return await LegendaryManager.GetLegendaryBuildIdAsync(appId);
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
