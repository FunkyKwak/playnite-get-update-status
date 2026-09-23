using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using GameUpdateStatus.Controls;
using Playnite.SDK.Events;
using Playnite.SDK.Data;
using System.Windows;

namespace GameUpdateStatus
{
    public class GameUpdateStatusPlugin : GenericPlugin
    {
        public const string ExtensionName = "GameUpdateStatus";
        
        public static GameUpdateStatusPlugin Instance { get; private set; }

        private readonly ILogger logger;
        private readonly SteamUpdateChecker steamChecker;
        private readonly EpicUpdateChecker epicChecker;

        private readonly Dictionary<string, UpdateStatus> statuses =
            new Dictionary<string, UpdateStatus>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler StatusesUpdated;
        private readonly string statusFile;

        public override Guid Id =>
            Guid.Parse("6D8E4F57-3B19-4A61-A2F4-8D0C5B9A7E21");


        public GameUpdateStatusSettings Settings;
        public override ISettings GetSettings(bool firstRunSettings)
        {
            return new GameUpdateStatusSettings(this);
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GameUpdateStatusSettingsView();
        }

        public GameUpdateStatusPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            Instance = this;
            Settings = (GameUpdateStatusSettings)GetSettings(false);
            logger = LogManager.GetLogger();

            steamChecker = new SteamUpdateChecker(logger);
            if (Settings.EnableSourceEpic)
                epicChecker = new EpicUpdateChecker(logger, api);

            statusFile = Path.Combine(
                GetPluginUserDataPath(),
                "update-status.json");

            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string>
                {
                    "UpdateStatus"
                },
                SourceName = ExtensionName
            });

            LoadStatusFile();
        }

        public override void OnApplicationStarted(
            OnApplicationStartedEventArgs args)
        {
            LoadStatusFile();

            _ = CheckForUpdatesAsync();
        }

        public override void OnApplicationStopped(
            OnApplicationStoppedEventArgs args)
        {
            Instance = null;
        }

        public override Control GetGameViewControl(
            GetGameViewControlArgs args)
        {
            if (args.Name == "UpdateStatus")
            {
                return new UpdateStatusControl();
            }
            return null;
        }

        public UpdateStatusComponent GetStatus(Game game)
        {
            if (game == null)
                return new UpdateStatusComponent(UpdateStatus.NotInstalled, "Jeu inconnu");

            string sourceName = GetSupportedSource(game.Source?.Name);

            if (string.IsNullOrWhiteSpace(sourceName))
                return new UpdateStatusComponent(UpdateStatus.NotInstalled, "Source non supportée (" + game.Source?.Name + ")", Settings.ShowRedDotOnUnsupportedSource ? Visibility.Visible : Visibility.Collapsed);

            if (sourceName == "Manual")
                return new UpdateStatusComponent(UpdateStatus.UpToDate, "Jeu ajouté manuellement (toujours à jour)");

            if (string.IsNullOrWhiteSpace(game.GameId))
                return new UpdateStatusComponent(UpdateStatus.NotInstalled, "ID de jeu invalide");

            string cacheKey = BuildStatusKey(sourceName, game.GameId);

            UpdateStatus status;

            if (statuses.TryGetValue(
                cacheKey,
                out status))
            {
                return new UpdateStatusComponent(status);
            }

            // Games not in manifest files are considered not installed 
            return new UpdateStatusComponent(UpdateStatus.NotInstalled);
        }

        private static string GetSupportedSource(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
                return null;

            if (sourceName.Equals("Steam", StringComparison.OrdinalIgnoreCase))
                return "Steam";

            if (sourceName.Equals("Epic", StringComparison.OrdinalIgnoreCase))
                return "Epic";

            if (sourceName.Equals("Epic Games", StringComparison.OrdinalIgnoreCase))
                return "Epic";

            if (sourceName.Equals("Epic Games Store", StringComparison.OrdinalIgnoreCase))
                return "Epic";

            if (sourceName.Equals("Emulation", StringComparison.OrdinalIgnoreCase))
                return "Manual";
            if (sourceName.Equals("Téléchargements", StringComparison.OrdinalIgnoreCase))
                return "Manual";

            return null;
        }

        private static string BuildStatusKey(string sourceName, string gameId)
        {
            return (sourceName ?? "Unknown") + ":" + (gameId ?? string.Empty);
        }


        public void ReloadSettings()
        {
            Settings = LoadPluginSettings<GameUpdateStatusSettings>();

            if (Settings == null)
            {
                Settings = new GameUpdateStatusSettings(this);
            }
        }

        public async void ForceCheckForUpdates()
        {
            await CheckForUpdatesAsync(true);
        }
        private async Task CheckForUpdatesAsync(bool force = false)
        {
            try
            {
                if (!force && IsCacheValid())
                {
                    logger.Info("Update cache still valid. No Steam check needed.");
                    return;
                }

                logger.Info("Update cache expired. Starting update checks.");

                List<StatusEntry> results = new List<StatusEntry>();

                results.AddRange(
                    await Task.Run(() => steamChecker.Check())
                );
                if (Settings.EnableSourceEpic)
                {
                    logger.Info("EPIC Games source enabled - checking...");
                    results.AddRange(
                        await Task.Run(() => epicChecker.Check())
                    );
                }

                if (results == null || results.Count == 0)
                {
                    logger.Warn("Update checks returned no results.");
                    return;
                }

                SaveResults(results);

                logger.Info(
                    "Update checks completed: " +
                    results.Count +
                    " games.");

                // Recharge le dictionnaire utilisé par les contrôles.
                LoadStatusFile();
                
                RefreshStatusControls();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Steam update check failed.");
            }
        }

        public void RefreshStatusControls()
        {
            StatusesUpdated?.Invoke(this, EventArgs.Empty);
        }

        private bool IsCacheValid()
        {
            try
            {
                if (!File.Exists(statusFile))
                    return false;

                DateTime lastWrite = File.GetLastWriteTime(statusFile);

                double ageMinutes = (DateTime.Now - lastWrite).TotalMinutes;

                logger.Info(
                    "Update cache age: " +
                    Math.Round(ageMinutes, 1) +
                    " minutes.");

                return ageMinutes < Settings.CacheDurationMinutes;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to check update cache.");
                return false;
            }
        }

        private void SaveResults(
            List<StatusEntry> results)
        {
            try
            {
                string directory = Path.GetDirectoryName(statusFile);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = Serialization.ToJson(results);

                File.WriteAllText(
                    statusFile,
                    json);

                logger.Info("Update status cache saved: " + statusFile);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save update status cache.");
            }
        }

        private void LoadStatusFile()
        {
            statuses.Clear();

            try
            {
                if (!File.Exists(statusFile))
                {
                    logger.Info("Update status file not found: " + statusFile);
                    return;
                }

                string json = File.ReadAllText(statusFile);

                var entries = Serialization.FromJson<List<StatusEntry>>(json);

                if (entries == null)
                    return;

                foreach (StatusEntry entry in entries)
                {
                    if (entry == null ||
                        string.IsNullOrWhiteSpace(entry.AppId))
                    {
                        continue;
                    }

                    statuses[BuildStatusKey(GetSupportedSource(entry.Source), entry.AppId)] =
                        ParseStatus(entry.Status);
                }

                logger.Info(
                    "Loaded " +
                    statuses.Count +
                    " update statuses.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load update status file.");
            }
        }

        private static UpdateStatus ParseStatus(
            string status)
        {
            return (UpdateStatus)Enum.Parse(typeof(UpdateStatus), status);
        }
    }
}