using System.Windows.Media;
using System.Windows;

namespace GameUpdateStatus
{
    public enum UpdateStatus
    {
        NotInstalled,
        Error,
        Unknown,
        Unsupported,
        UpToDate,
        UpdateAvailable
    }

    public class UpdateStatusComponent
    {
        public UpdateStatus Status { get; set; }

        public string StatusText { get; set; }
        public Brush StatusBrush { get; set; }
        public Visibility Visibility { get; set; }

        public UpdateStatusComponent(UpdateStatus status, string statusText = null, Visibility? visibility = null)
        {
            Status = status;
            switch (status)
            {
                case UpdateStatus.UpToDate:
                    StatusBrush = Brushes.LimeGreen;
                    StatusText = statusText ?? "À jour";
                    Visibility = visibility ?? Visibility.Visible;
                    break;

                case UpdateStatus.UpdateAvailable:
                    StatusBrush = Brushes.Orange;
                    StatusText = statusText ?? "Mise à jour disponible";
                    Visibility = visibility ?? Visibility.Visible;
                    break;

                case UpdateStatus.NotInstalled:
                    StatusBrush = Brushes.Gray;
                    StatusText = statusText ?? "Non installé";
                    // Visibility = Visibility.Visible;
                    Visibility = visibility ?? Visibility.Collapsed;
                    break;

                case UpdateStatus.Unsupported:
                    StatusBrush = Brushes.Red;
                    StatusText = $"Source non supportée ({statusText})";
                    Visibility = visibility ?? Visibility.Visible;
                    break;

                case UpdateStatus.Error:
                    StatusBrush = Brushes.Red;
                    StatusText = statusText ?? "Erreur inconnue";
                    Visibility = visibility ?? Visibility.Visible;
                    break;

                case UpdateStatus.Unknown:
                default:
                    StatusBrush = Brushes.Gray;
                    StatusText = statusText ?? "État inconnu";
                    Visibility = visibility ?? Visibility.Visible;
                    break;
            };
        }
    }   
}