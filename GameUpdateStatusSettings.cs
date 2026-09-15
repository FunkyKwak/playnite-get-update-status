using Playnite.SDK;

namespace GameUpdateStatus
{
    public class GameUpdateStatusSettings : System.Collections.Generic.ObservableObject
    {
        private int cacheDurationMinutes = 30;

        public int CacheDurationMinutes
        {
            get => cacheDurationMinutes;
            set => SetValue(ref cacheDurationMinutes, value);
        }
    }
}