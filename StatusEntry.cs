namespace GameUpdateStatus
{
    public class StatusEntry
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string LocalBuild { get; set; }
        public string PublicBuild { get; set; }
        public string Status { get; set; }
        public string CheckedAt { get; set; }
    }
}