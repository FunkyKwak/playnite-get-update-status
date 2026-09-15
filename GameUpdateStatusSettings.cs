using Playnite.SDK;

namespace GameUpdateStatus
{
    public class GameUpdateStatusSettings : ObservableObject
    {
        private int cacheDurationMinutes = 30;

        public int CacheDurationMinutes
        {
            get => cacheDurationMinutes;
            set => SetValue(ref cacheDurationMinutes, value);
        }
    }
}