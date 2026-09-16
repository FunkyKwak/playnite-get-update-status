using System.Windows.Media;
using System.Windows;

namespace GameUpdateStatus
{
    public enum UpdateStatus
    {
        NotInstalled,
        Unknown,
        UpToDate,
        UpdateAvailable
    }

    public class UpdateStatusComponent
    {
        public UpdateStatus Status { get; set; }

        public string StatusText { get; set; }
        public Brush StatusBrush { get; set; }
        public Visibility Visibility { get; set; }

        public UpdateStatusComponent(UpdateStatus status, string statusText = null)
        {
            Status = status;
            switch (status)
            {
                case UpdateStatus.UpToDate:
                    StatusBrush = Brushes.LimeGreen;
                    StatusText = statusText ?? "À jour";
                    Visibility = Visibility.Visible;
                    break;

                case UpdateStatus.UpdateAvailable:
                    StatusBrush = Brushes.Orange;
                    StatusText = statusText ?? "Mise à jour disponible";
                    Visibility = Visibility.Visible;
                    break;

                case UpdateStatus.NotInstalled:
                    StatusBrush = Brushes.Red;
                    StatusText = statusText ?? "Non installé";
                    Visibility = Visibility.Visible;
                    // Visibility = Visibility.Collapsed;
                    break;

                case UpdateStatus.Unknown:
                default:
                    StatusBrush = Brushes.Gray;
                    StatusText = statusText ?? "État inconnu";
                    Visibility = Visibility.Visible;
                    break;
            };
        }
    }   
}