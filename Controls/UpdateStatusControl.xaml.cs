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
            catch (Exception ex)
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