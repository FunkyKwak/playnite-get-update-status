using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Controls;

namespace GameUpdateStatus.Controls
{
    public partial class UpdateStatusControl : PluginUserControl
    {
        public Brush StatusBrush { get; private set; }

        public string StatusText { get; private set; }

        public UpdateStatusControl()
        {
            InitializeComponent();

            DataContext = this;

            UpdateStatus();
        }

        protected override void GameContextChanged(
            Game oldContext,
            Game newContext)
        {
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var game = GameContext;

            if (game == null ||
                GameUpdateStatusPlugin.Instance == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            var status =
                GameUpdateStatusPlugin.Instance.GetStatus(game);

            switch (status)
            {
                case UpdateStatus.UpToDate:

                    StatusBrush = Brushes.LimeGreen;
                    StatusText = "À jour";
                    Visibility = Visibility.Visible;

                    break;

                case UpdateStatus.UpdateAvailable:

                    StatusBrush = Brushes.Orange;
                    StatusText = "Mise à jour disponible";
                    Visibility = Visibility.Visible;

                    break;

                case UpdateStatus.Unknown:

                    StatusBrush = Brushes.Gray;
                    StatusText = "État inconnu";
                    Visibility = Visibility.Visible;

                    break;

                default:

                    Visibility = Visibility.Collapsed;

                    break;
            }

            // Force la mise à jour du binding.
            DataContext = null;
            DataContext = this;
        }
    }
}