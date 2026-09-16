using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Playnite.SDK;

namespace GameUpdateStatus
{
    public class EpicUpdateChecker : Checker
    {
        public EpicUpdateChecker(ILogger logger) : base(logger) { }

        public override List<StatusEntry> Check()
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
                    results.Add(CheckSingle(manifest));
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Impossible de lire un manifeste Epic : " + manifest);
                }
            }

            return results;
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
            string appId = GetValue(content, "CatalogItemId") ?? GetValue(content, "AppName") ?? Path.GetFileNameWithoutExtension(manifest);
            string localBuild = GetValue(content, "AppVersionString") ?? GetValue(content, "BuildLabel") ?? GetValue(content, "BuildVersion");
            string catalogNamespace = GetValue(content, "CatalogNamespace") ?? "";
            string storeUrl = GetValue(content, "MainGameUrl") ?? GetValue(content, "StoreUrl") ?? GetValue(content, "EpicStoreUrl") ?? GetValue(content, "URL");

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
        protected override string GetPublicBuildId(string key)
        {
            string[] keys = key.Split(':');

            if (string.IsNullOrWhiteSpace(keys[1]))
                return null;

            string graphqlBuild = GetBuildVersion("Windows", keys[1], "76233c0af0a441c7bd541e7d7ab3cde1");
            if (!string.IsNullOrWhiteSpace(graphqlBuild))
                return graphqlBuild;

            logger.Info("Aucune version publique fiable trouvée pour " + keys[1] + ". Statut conservé : INCONNU.");
            return null;
        }



        private string GetBuildVersion(string platform, string catalogItemId, string appName)
        {
            logger.Info("Epic GraphQL Authentication"); 

            var clientId = Environment.GetEnvironmentVariable("EPIC_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("EPIC_CLIENT_SECRET");
            var authUrl = "https://api.epicgames.dev/auth/v1/oauth/token";

            var authRequest = (HttpWebRequest)WebRequest.Create(authUrl);
            authRequest.Method = "POST";
            authRequest.ContentType = "application/x-www-form-urlencoded";
            authRequest.Timeout = 15000;

            // 2. Ajouter l'authentification Basic
            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            authRequest.Headers.Add("Authorization", $"Basic {authToken}");

            // 3. Écrire le body (grant_type et scope)
            var authData = $"grant_type=client_credentials&scope=launcher:download:Live:{appName} READ";
            var authBytes = Encoding.UTF8.GetBytes(authData);
            authRequest.ContentLength = authBytes.Length;

            using (var requestStream = authRequest.GetRequestStream())
            {
                requestStream.Write(authBytes, 0, authBytes.Length);
            }

            try
            {
                var authResponse = (HttpWebResponse)authRequest.GetResponse();
                using (var responseStream = authResponse.GetResponseStream())
                using (var reader = new StreamReader(responseStream))
                {
                    var authJson = reader.ReadToEnd();
                    Console.WriteLine($"Réponse auth: {authJson}");

                    // 5. Parser le token et appeler GetAsset
                    logger.Info("GraphQL Epic Authentication response: " + authJson.Substring(0, Math.Min(300, authJson.Length)));
                    var accessToken = ExtractAccessToken(authJson);


                    return GetBuildVersion(platform, catalogItemId, appName, accessToken);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var errorStream = ex.Response.GetResponseStream())
                    using (var errorReader = new StreamReader(errorStream))
                    {
                        var errorJson = errorReader.ReadToEnd();
                        Console.WriteLine($"Erreur: {ex.Status} - {errorJson}");
                    }
                }
                else
                {
                    Console.WriteLine($"Erreur: {ex.Message}");
                }
            }

            return null;

        }

        private string GetBuildVersion(string platform, string catalogItemId, string appName, string accessToken)
        {
            var url = $"https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/public/assets/{platform}/{catalogItemId}/{appName}";
            logger.Info("Epic GraphQL lookup: " + url); 
            logger.Info("using token: " + accessToken); 

            var request = WebRequest.CreateHttp(url);
            request.Method = "GET";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.Timeout = 15000;
            request.Headers.Add("Authorization", "Bearer " + accessToken);


            try
            {
                using (var response = (HttpWebResponse)request.GetResponse()) {
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        logger.Info("GraphQL Epic response: " + json.Substring(0, Math.Min(300, json.Length)));
                        return ExtractEpicBuildId(json);
                    }
                }
            }
            catch(Exception ex)
            {
                logger.Info("Epic GraphQL lookup failed for " + catalogItemId + " : " + ex.Message);
            }
            return null;
        }

        private string ExtractEpicBuildId(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            string[] keys = { "appVersionString", "versionString", "buildVersion", "buildId", "appVersion" };

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

        private string ExtractAccessToken(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            string[] keys = { "access_token" };

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
