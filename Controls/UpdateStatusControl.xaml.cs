using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Playnite.SDK.Models;

namespace GameUpdateStatus.Controls
{
    public partial class UpdateStatusControl : UserControl
    {
        private Game game;

        public Game GameContext
        {
            get => game;
            set
            {
                game = value;
                UpdateVisual();
            }
        }

        public Brush StatusBrush { get; private set; }

        public string StatusText { get; private set; }

        public UpdateStatusControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UpdateVisual()
        {
            if (game == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            var plugin = GameUpdateStatusPlugin.Instance;

            if (plugin == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            var status = plugin.GetStatus(game);

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

            // Force WPF à actualiser les bindings.
            DataContext = null;
            DataContext = this;
        }
    }
}