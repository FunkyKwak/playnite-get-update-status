using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

using Playnite.SDK;
using Playnite.SDK.Models;

using GameUpdateStatus.Controls;

namespace GameUpdateStatus
{
    public class GameUpdateStatusPlugin : GenericPlugin
    {
        public static GameUpdateStatusPlugin Instance { get; private set; }

        private readonly ILogger logger;

        private readonly Dictionary<string, UpdateStatus> statuses =
            new Dictionary<string, UpdateStatus>();

        private GameUpdateStatusSettings settings;

        public override Guid Id =>
            Guid.Parse("6D8E4F57-3B19-4A61-A2F4-8D0C5B9A7E21");

        public GameUpdateStatusPlugin(IPlayniteAPI api) : base(api)
        {
            Instance = this;

            logger = LogManager.GetLogger();

            settings = LoadPluginSettings<GameUpdateStatusSettings>()
                       ?? new GameUpdateStatusSettings();

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

        public UpdateStatus GetStatus(Game game)
        {
            if (game == null)
                return UpdateStatus.Unknown;

            /*
             * Pour Steam :
             *
             * game.Source = Steam
             * game.GameId = Steam AppID
             */

            if (game.Source == null ||
                !game.Source.Name.Equals(
                    "Steam",
                    StringComparison.OrdinalIgnoreCase))
            {
                return UpdateStatus.Unknown;
            }

            if (string.IsNullOrEmpty(game.GameId))
                return UpdateStatus.Unknown;

            if (statuses.TryGetValue(game.GameId, out var status))
                return status;

            /*
             * Aucun résultat signifie notamment :
             * - jeu non installé
             * - jeu non vérifié
             *
             * On ne veut PAS afficher de boule dans ce cas.
             */

            return UpdateStatus.Unknown;
        }

        private void LoadStatusFile()
        {
            statuses.Clear();

            try
            {
                var file = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "Playnite",
                    "Extensions",
                    "GameUpdateStatus",
                    "update-status.json");

                if (!File.Exists(file))
                {
                    logger.Info(
                        $"Update status file not found: {file}");

                    return;
                }

                var json = File.ReadAllText(file);

                var entries =
                    Newtonsoft.Json.JsonConvert
                        .DeserializeObject<List<StatusEntry>>(json);

                if (entries == null)
                    return;

                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.AppId))
                    {
                        statuses[entry.AppId] =
                            ParseStatus(entry.Status);
                    }
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

                default:
                    return UpdateStatus.Unknown;
            }
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

        public override void AddCustomElementSupport(
            AddCustomElementSupportArgs args)
        {
            args.AddElement(
                "UpdateStatus",
                "UpdateStatus");
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