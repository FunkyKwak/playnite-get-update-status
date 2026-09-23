using System.Windows;
using System.Windows.Controls;

namespace GameUpdateStatus
{
    public partial class GameUpdateStatusSettingsView : UserControl
    {
        public GameUpdateStatusSettingsView()
        {
            InitializeComponent();
        }
        
        private void CheckForUpdates_Click(
            object sender,
            RoutedEventArgs e)
        {
            GameUpdateStatusPlugin.Instance?.ForceCheckForUpdates();
        }
    }
}