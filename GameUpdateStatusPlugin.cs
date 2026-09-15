using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

using GameUpdateStatus.Controls;

namespace GameUpdateStatus
{
    public class GameUpdateStatusPlugin : GenericPlugin
    {
        public const string ExtensionName = "GameUpdateStatus";

        public static GameUpdateStatusPlugin Instance { get; private set; }

        private readonly ILogger logger;

        private readonly Dictionary<string, UpdateStatus> statuses =
            new Dictionary<string, UpdateStatus>(
                StringComparer.OrdinalIgnoreCase);

        public override Guid Id =>
            Guid.Parse("6D8E4F57-3B19-4A61-A2F4-8D0C5B9A7E21");

        public GameUpdateStatusPlugin(IPlayniteAPI api)
            : base(api)
        {
            Instance = this;

            logger = LogManager.GetLogger();

            AddCustomElementSupport(
                new AddCustomElementSupportArgs
                {
                    SourceName = ExtensionName,
                    ElementList = new List<string>
                    {
                        "UpdateStatus"
                    }
                });

            LoadStatusFile();
        }

        public override void OnApplicationStarted(
            OnApplicationStartedEventArgs args)
        {
            LoadStatusFile();
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

        public UpdateStatus GetStatus(Game game)
        {
            if (game == null)
            {
                return UpdateStatus.NotInstalled;
            }

            // Pour l'instant, uniquement Steam.
            if (game.Source == null ||
                !game.Source.Name.Equals(
                    "Steam",
                    StringComparison.OrdinalIgnoreCase))
            {
                return UpdateStatus.NotInstalled;
            }

            if (string.IsNullOrWhiteSpace(game.GameId))
            {
                return UpdateStatus.NotInstalled;
            }

            if (!statuses.TryGetValue(
                    game.GameId,
                    out var status))
            {
                // Pas présent dans le fichier = pas installé.
                return UpdateStatus.NotInstalled;
            }

            return status;
        }

        private void LoadStatusFile()
        {
            statuses.Clear();

            try
            {
                string file = Path.Combine(
                    GetPluginUserDataPath(),
                    "update-status.json");

                if (!File.Exists(file))
                {
                    logger.Info(
                        $"Update status file not found: {file}");

                    return;
                }

                string json =
                    File.ReadAllText(file);

                var entries =
                    Serialization.FromJson<List<StatusEntry>>(
                        json);

                if (entries == null)
                {
                    return;
                }

                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.AppId))
                    {
                        continue;
                    }

                    statuses[entry.AppId] =
                        ParseStatus(entry.Status);
                }

                logger.Info(
                    $"Loaded {statuses.Count} update statuses.");
            }
            catch (Exception ex)
            {
                logger.Error(
                    ex,
                    "Failed to load update status file.");
            }
        }

        private UpdateStatus ParseStatus(string status)
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

        private class StatusEntry
        {
            public string AppId { get; set; }

            public string Name { get; set; }

            public string LocalBuild { get; set; }

            public string PublicBuild { get; set; }

            public string Status { get; set; }

            public string CheckedAt { get; set; }
        }
    }
}