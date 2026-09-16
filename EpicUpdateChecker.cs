using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
            string storeUrl = GetValue(content, "MainGameUrl") ?? GetValue(content, "StoreUrl") ?? GetValue(content, "EpicStoreUrl") ?? GetValue(content, "URL");

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

            string publicBuild = GetEpicPublicBuildId(catalogNamespace, appId, storeUrl);
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

        private string GetEpicPublicBuildId(string catalogNamespace, string catalogItemId, string storeUrl = null)
        {
            if (string.IsNullOrWhiteSpace(catalogItemId))
                return null;

            string[] urls = BuildEpicLookupUrls(catalogNamespace, catalogItemId);

            foreach (string url in urls)
            {
                try
                {
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
                        string publicBuild = ExtractEpicBuildId(json);
                        if (!string.IsNullOrWhiteSpace(publicBuild))
                            return publicBuild;
                    }
                }
                catch (Exception ex)
                {
                    logger.Info("Epic public lookup failed for " + catalogItemId + " on " + url + " : " + ex.Message);
                }
            }

            string pageBuild = TryEpicStorePageLookup(storeUrl);
            if (!string.IsNullOrWhiteSpace(pageBuild))
                return pageBuild;

            string graphqlBuild = GetBuildVersion("Windows", catalogItemId, "76233c0af0a441c7bd541e7d7ab3cde1");
            if (!string.IsNullOrWhiteSpace(graphqlBuild))
                return graphqlBuild;

            string epicStoreApiBuild = TryEpicStoreApiLookup(catalogItemId);
            if (!string.IsNullOrWhiteSpace(epicStoreApiBuild))
                return epicStoreApiBuild;

            string legendaryBuild = TryLegendaryLookup(catalogItemId);
            if (!string.IsNullOrWhiteSpace(legendaryBuild))
                return legendaryBuild;

            logger.Info("Aucune version publique fiable trouvée pour " + catalogItemId + ". Statut conservé : INCONNU.");
            return null;
        }

        private string TryEpicStorePageLookup(string productUrl)
        {
            if (string.IsNullOrWhiteSpace(productUrl))
                return null;

            try
            {
                var request = WebRequest.CreateHttp(productUrl);
                request.Method = "GET";
                request.Timeout = 15000;
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string html = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(html))
                        return null;

                    logger.Info("Epic store page lookup: " + productUrl);

                    string[] candidateKeys = { "appVersionString", "versionString", "buildVersion", "buildId", "appVersion" };
                    foreach (string key in candidateKeys)
                    {
                        Match match = Regex.Match(
                            html,
                            "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"",
                            RegexOptions.IgnoreCase | RegexOptions.Singleline);

                        if (match.Success)
                            return match.Groups[1].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Info("Epic store page lookup failed for " + productUrl + " : " + ex.Message);
            }

            return null;
        }

        private string TryGraphQlLookup(string catalogNamespace, string catalogItemId)
        {
            try
            {   
                string url = "https://graphql.epicgames.com/graphql";
                var request = WebRequest.CreateHttp(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Accept = "application/json";
                request.Timeout = 15000;


                string gameName = "unrailed";
                
                string query = $@"{{
                Catalog {{
                    searchStore(keywords: ""{gameName}"", count: 1) {{
                    elements {{
                        id
                        title
                        namespace
                    }}
                    }}
                }}
                }}";

                byte[] payload = Encoding.UTF8.GetBytes(query);
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(payload, 0, payload.Length);
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    logger.Info("GraphQL Epic response: " + json.Substring(0, Math.Min(300, json.Length)));
                    return ExtractEpicBuildId(json);
                }
            }
            catch (Exception ex)
            {
                logger.Info("GraphQL Epic lookup failed for " + catalogItemId + " : " + ex.Message);
                return null;
            }
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

        private string TryEpicStoreApiLookup(string catalogItemId)
        {
            try
            {
                string url = "https://store-site-backend-static.ak.epicgames.com/api/marketplace/v2/catalog?locale=fr-FR";
                var request = WebRequest.CreateHttp(url);
                request.Method = "GET";
                request.Timeout = 15000;
                request.Accept = "application/json";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    string json = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(json))
                        return null;

                    string pattern = "\"id\"\\s*:\\s*\"" + Regex.Escape(catalogItemId) + "\".*?\"appVersionString\"\\s*:\\s*\"([^\"]+)\"";
                    Match match = Regex.Match(json, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                logger.Info("Epic Store tierce API lookup failed for " + catalogItemId + " : " + ex.Message);
            }

            return null;
        }

        private string TryLegendaryLookup(string catalogItemId)
        {
            try
            {
                string[] candidates =
                {
                    "legendary",
                    "legendary.exe"
                };

                string executablePath = null;
                string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

                foreach (string candidate in candidates)
                {
                    foreach (string entry in pathEnv.Split(Path.PathSeparator))
                    {
                        string p = Path.Combine(entry, candidate);
                        if (File.Exists(p))
                        {
                            executablePath = p;
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(executablePath))
                        break;
                }

                if (string.IsNullOrWhiteSpace(executablePath))
                    return null;

                var proc = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(proc))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        logger.Info("Legendary detected: " + output.Trim());
                        return null;
                    }

                    logger.Info("Legendary not usable for version lookup: " + error.Trim());
                }
            }
            catch (Exception ex)
            {
                logger.Info("Legendary lookup failed: " + ex.Message);
            }

            return null;
        }

        private string[] BuildEpicLookupUrls(string catalogNamespace, string catalogItemId)
        {
            var urls = new List<string>();

            if (!string.IsNullOrWhiteSpace(catalogNamespace) && !string.IsNullOrWhiteSpace(catalogItemId))
            {
                urls.Add(
                    "https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/" +
                    catalogNamespace +
                    "/item/" +
                    catalogItemId +
                    "?locale=fr-FR");

                urls.Add(
                    "https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/" +
                    catalogNamespace +
                    "/item/" +
                    catalogItemId +
                    "?locale=en-US");
            }

            if (!string.IsNullOrWhiteSpace(catalogNamespace) && !string.IsNullOrWhiteSpace(catalogItemId))
            {
                urls.Add(
                    "https://store.epicgames.com/api/marketplace/items/" +
                    catalogNamespace +
                    "/" +
                    catalogItemId +
                    "?locale=fr-FR");

                urls.Add(
                    "https://store-site-backend-static.ak.epicgames.com/api/marketplace/v2/item/" +
                    catalogNamespace +
                    "/" +
                    catalogItemId +
                    "?locale=fr-FR");
            }

            return urls
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
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
