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

namespace GameUpdateStatus
{
    public class GameUpdateStatusPlugin : GenericPlugin
    {
        public const string ExtensionName = "GameUpdateStatus";

        public static GameUpdateStatusPlugin Instance { get; private set; }

        private readonly ILogger logger;
        private readonly SteamUpdateChecker steamChecker;

        private readonly Dictionary<string, UpdateStatus> statuses =
            new Dictionary<string, UpdateStatus>(
                StringComparer.OrdinalIgnoreCase);

        public event EventHandler StatusesUpdated;
        private readonly string statusFile;

        private const int CacheDurationMinutes = 30;

        public override Guid Id =>
            Guid.Parse("6D8E4F57-3B19-4A61-A2F4-8D0C5B9A7E21");

        public GameUpdateStatusPlugin(IPlayniteAPI api)
            : base(api)
        {
            Instance = this;

            logger = LogManager.GetLogger();

            steamChecker = new SteamUpdateChecker(logger);

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
                logger.Info(
                    "GetGameViewControl : UpdateStatus");

                return new UpdateStatusControl();
            }

            return null;
        }

        public UpdateStatusComponent GetStatus(Game game)
        {
            if (game == null)
                return new UpdateStatusComponent(UpdateStatus.NotInstalled, "Jeu inconnu");

            if (game.Source == null ||
                !game.Source.Name.Equals(
                    "Steam",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new UpdateStatusComponent(UpdateStatus.NotInstalled, "Source non supportée");
            }

            if (string.IsNullOrWhiteSpace(game.GameId))
                return new UpdateStatusComponent(UpdateStatus.NotInstalled, "ID de jeu invalide");

            UpdateStatus status;

            if (statuses.TryGetValue(
                game.GameId,
                out status))
            {
                return new UpdateStatusComponent(status);
            }

            return new UpdateStatusComponent(UpdateStatus.Unknown);
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                if (IsCacheValid())
                {
                    logger.Info(
                        "Update cache still valid. No Steam check needed.");

                    return;
                }

                logger.Info(
                    "Update cache expired. Starting Steam update check.");

                List<StatusEntry> results =
                    await Task.Run(() => steamChecker.Check());

                if (results == null ||
                    results.Count == 0)
                {
                    logger.Warn(
                        "Steam update check returned no results.");

                    return;
                }

                SaveResults(results);

                logger.Info(
                    "Steam update check completed: " +
                    results.Count +
                    " games.");

                // Recharge le dictionnaire utilisé par les contrôles.
                LoadStatusFile();
                
                StatusesUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(
                    ex,
                    "Steam update check failed.");
            }
        }

        private bool IsCacheValid()
        {
            try
            {
                if (!File.Exists(statusFile))
                    return false;

                DateTime lastWrite =
                    File.GetLastWriteTime(statusFile);

                double ageMinutes =
                    (DateTime.Now - lastWrite).TotalMinutes;

                logger.Info(
                    "Update cache age: " +
                    Math.Round(ageMinutes, 1) +
                    " minutes.");

                return ageMinutes < CacheDurationMinutes;
            }
            catch (Exception ex)
            {
                logger.Error(
                    ex,
                    "Failed to check update cache.");

                return false;
            }
        }

        private void SaveResults(
            List<StatusEntry> results)
        {
            try
            {
                string directory =
                    Path.GetDirectoryName(statusFile);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json =
                    Serialization.ToJson(results);

                File.WriteAllText(
                    statusFile,
                    json);

                logger.Info(
                    "Update status cache saved: " +
                    statusFile);
            }
            catch (Exception ex)
            {
                logger.Error(
                    ex,
                    "Failed to save update status cache.");
            }
        }

        private void LoadStatusFile()
        {
            statuses.Clear();

            try
            {
                if (!File.Exists(statusFile))
                {
                    logger.Info(
                        "Update status file not found: " +
                        statusFile);

                    return;
                }

                string json =
                    File.ReadAllText(statusFile);

                var entries =
                    Serialization.FromJson<List<StatusEntry>>(
                        json);

                if (entries == null)
                    return;

                foreach (StatusEntry entry in entries)
                {
                    if (entry == null ||
                        string.IsNullOrWhiteSpace(entry.AppId))
                    {
                        continue;
                    }

                    statuses[entry.AppId] =
                        ParseStatus(entry.Status);
                }

                logger.Info(
                    "Loaded " +
                    statuses.Count +
                    " update statuses.");
            }
            catch (Exception ex)
            {
                logger.Error(
                    ex,
                    "Failed to load update status file.");
            }
        }

        private static UpdateStatus ParseStatus(
            string status)
        {
            switch (status)
            {
                case "UP_TO_DATE":
                    return UpdateStatus.UpToDate;

                case "UPDATE_AVAILABLE":
                    return UpdateStatus.UpdateAvailable;

                case "UNKNOWN":
                    return UpdateStatus.Unknown;

                default:
                    return UpdateStatus.Unknown;
            }
        }
    }
}