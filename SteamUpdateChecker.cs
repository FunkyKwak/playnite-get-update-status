using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Playnite.SDK;

namespace GameUpdateStatus
{
    public class SteamUpdateChecker
    {
        private readonly ILogger logger;

        public SteamUpdateChecker(ILogger logger)
        {
            this.logger = logger;
        }

        public List<StatusEntry> Check()
        {
            var results = new List<StatusEntry>();

            string steamPath = FindSteam();

            if (string.IsNullOrWhiteSpace(steamPath))
            {
                logger.Error("Steam introuvable.");
                return results;
            }

            logger.Info("Steam : " + steamPath);

            var libraries = FindLibraries(steamPath);

            logger.Info("Bibliothèques trouvées : " + libraries.Count);

            var manifests = new List<string>();

            foreach (string library in libraries)
            {
                try
                {
                    manifests.AddRange(
                        Directory.GetFiles(
                            library,
                            "appmanifest_*.acf",
                            SearchOption.TopDirectoryOnly));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Impossible de lire la bibliothèque Steam : " + library);
                }
            }

            logger.Info("Jeux Steam installés : " + manifests.Count);

            foreach (string manifest in manifests)
            {
                StatusEntry result = CheckManifest(manifest);
                results.Add(result);
            }

            return results;
        }

        private string FindSteam()
        {
            string steamPath = @"C:\Program Files (x86)\Steam";

            if (Directory.Exists(steamPath))
                return steamPath;

            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    string path = key?.GetValue("InstallPath") as string;

                    if (!string.IsNullOrWhiteSpace(path) &&
                        Directory.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Erreur lors de la recherche de Steam dans le registre.");
            }

            return null;
        }

        private List<string> FindLibraries(string steamPath)
        {
            var libraries = new List<string>();

            string mainSteamApps = Path.Combine(steamPath, "steamapps");

            if (Directory.Exists(mainSteamApps))
                libraries.Add(mainSteamApps);

            string libraryFile =
                Path.Combine(mainSteamApps, "libraryfolders.vdf");

            if (!File.Exists(libraryFile))
                return libraries;

            try
            {
                string content = File.ReadAllText(libraryFile);

                MatchCollection matches = Regex.Matches(
                    content,
                    "\"path\"\\s+\"([^\"]+)\"");

                foreach (Match match in matches)
                {
                    string path = match.Groups[1].Value
                        .Replace(@"\\", @"\");

                    string steamApps =
                        Path.Combine(path, "steamapps");

                    if (Directory.Exists(steamApps) &&
                        !libraries.Contains(
                            steamApps,
                            StringComparer.OrdinalIgnoreCase))
                    {
                        libraries.Add(steamApps);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Impossible de lire libraryfolders.vdf.");
            }

            return libraries;
        }

        private StatusEntry CheckManifest(string manifest)
        {
            string fileName = Path.GetFileNameWithoutExtension(manifest);

            string appId =
                fileName.Replace("appmanifest_", "");

            string content = File.ReadAllText(manifest);

            string name = GetValue(content, "name")
                          ?? "AppID " + appId;

            string localBuild = GetValue(content, "buildid");

            var result = new StatusEntry
            {
                Source = "Steam",
                AppId = appId,
                Name = name,
                LocalBuild = localBuild,
                PublicBuild = null,
                Status = UpdateStatus.Unknown.ToString(),
                CheckedAt = DateTime.Now.ToString("o")
            };

            if (string.IsNullOrWhiteSpace(localBuild))
                return result;

            logger.Info(
                "[" + appId + "] " + name +
                " - Local : " + localBuild);

            string publicBuild = GetSteamPublicBuildId(appId);

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

        private string GetSteamPublicBuildId(string appId)
        {
            try
            {
                string url =
                    "https://api.steamcmd.net/v1/info/" + appId;

                var request = WebRequest.CreateHttp(url);
                request.Method = "GET";
                request.Timeout = 15000;

                using (var response =
                    (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();

                    return ExtractPublicBuildId(json, appId);
                }
            }
            catch (Exception ex)
            {
                logger.Info(
                    "Impossible de récupérer le BuildID public pour " +
                    appId + " : " + ex.Message);

                return null;
            }
        }

        private string ExtractPublicBuildId(string json, string appId)
        {
            // On cherche :
            // "public": {
            //     ...
            //     "buildid": "123456"

            string pattern =
                "\"public\"\\s*:\\s*\\{.*?\"buildid\"\\s*:\\s*\"([^\"]+)\"";

            Match match = Regex.Match(
                json,
                pattern,
                RegexOptions.Singleline);

            return match.Success
                ? match.Groups[1].Value
                : null;
        }

        private string GetValue(string content, string key)
        {
            Match match = Regex.Match(
                content,
                "\"" + Regex.Escape(key) +
                "\"\\s+\"([^\"]*)\"");

            return match.Success
                ? match.Groups[1].Value
                : null;
        }
    }
}