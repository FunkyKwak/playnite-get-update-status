using System;
using System.Windows;
using System.Windows.Media;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;

namespace GameUpdateStatus.Controls
{
    public partial class UpdateStatusControl : PluginUserControl
    {
        public Brush StatusBrush { get; private set; }
        public string StatusText { get; private set; }

        public UpdateStatusControl()
        {
            InitializeComponent();

            StatusBrush = Brushes.Gray;
            StatusText = "État inconnu";

            DataContext = this;

            if (GameUpdateStatusPlugin.Instance != null)
            {
                GameUpdateStatusPlugin.Instance.StatusesUpdated +=
                    OnStatusesUpdated;
            }
        }

        private void OnStatusesUpdated(
            object sender,
            EventArgs e)
        {
            RefreshStatus(GameContext);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (GameUpdateStatusPlugin.Instance != null)
            {
                GameUpdateStatusPlugin.Instance.StatusesUpdated -=
                    OnStatusesUpdated;

                GameUpdateStatusPlugin.Instance.StatusesUpdated +=
                    OnStatusesUpdated;
            }

            RefreshStatus(GameContext);
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (GameUpdateStatusPlugin.Instance != null)
            {
                GameUpdateStatusPlugin.Instance.StatusesUpdated -=
                    OnStatusesUpdated;
            }
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            RefreshStatus(newContext);
        }

        private void RefreshStatus(Game game)
        {
            StatusBrush = Brushes.Gray;
            StatusText = "État inconnu";
            Visibility = Visibility.Visible;
            RefreshBinding();
            
            if (GameUpdateStatusPlugin.Instance == null)
            {
                StatusBrush = Brushes.Gray;
                StatusText = "Extension GameUpdateStatus non chargée";
                Visibility = Visibility.Visible;
                RefreshBinding();
                return;
            }

            if (game == null)
            {
                StatusBrush = Brushes.Gray;
                StatusText = "Jeu inconnu";
                Visibility = Visibility.Visible;
                RefreshBinding();
                return;
            }

            UpdateStatusComponent status = new UpdateStatusComponent(UpdateStatus.Unknown);
            try    
            {
                status = GameUpdateStatusPlugin.Instance?.GetStatus(game) ?? new UpdateStatusComponent(UpdateStatus.Unknown);
            }
            catch (Exception)
            {
                StatusBrush = Brushes.Red;
                StatusText = "Erreur lors de la récupération du statut de mise à jour";
                Visibility = Visibility.Visible;
                RefreshBinding();
                return;
            }

            StatusBrush = status.StatusBrush;
            StatusText = status.StatusText;
            Visibility = status.Visibility;
            RefreshBinding();
        }

        private void RefreshBinding()
        {
            DataContext = null;
            DataContext = this;
        }
    }
}