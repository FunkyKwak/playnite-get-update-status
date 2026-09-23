using System.Collections.Generic;
using Playnite.SDK;

namespace GameUpdateStatus
{
    public class GameUpdateStatusSettings : ObservableObject, ISettings
    {
        private GameUpdateStatusPlugin plugin;


        private int cacheDurationMinutes;
        public int CacheDurationMinutes
        {
            get => cacheDurationMinutes;
            set => SetValue(ref cacheDurationMinutes, value);
        }

        private bool showDotOnUnsupportedSource;
        public bool ShowDotOnUnsupportedSource
        {
            get => showDotOnUnsupportedSource;
            set => SetValue(ref showDotOnUnsupportedSource, value);
        }

        private bool enableSourceEpic;
        public bool EnableSourceEpic
        {
            get => enableSourceEpic;
            set => SetValue(ref enableSourceEpic, value);
        }


        public GameUpdateStatusSettings()
        {
            EnableSourceEpic = true;
            ShowDotOnUnsupportedSource = true;
            CacheDurationMinutes = 30;
        }
        public GameUpdateStatusSettings(GameUpdateStatusPlugin plugin)
        {
            this.plugin = plugin;

            var savedSettings = plugin.LoadPluginSettings<GameUpdateStatusSettings>();

            if (savedSettings != null)
            {
                EnableSourceEpic = savedSettings.EnableSourceEpic;
                ShowDotOnUnsupportedSource = savedSettings.ShowDotOnUnsupportedSource;
                CacheDurationMinutes = savedSettings.CacheDurationMinutes;
            }
        }

        public void BeginEdit()
        {
        }

        public void CancelEdit()
        {
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(this);
            plugin.ReloadSettings();
            plugin.RefreshStatusControls();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (CacheDurationMinutes < 1)
            {
                errors.Add("Cache duration must be at least 1 minute.");
            }
            return errors.Count == 0;
        }
    }
}