namespace OrangLauncher.Models
{
    public class ServerInfo
    {
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        public bool IsHidden { get; set; } = false;
        public string? Icon { get; set; }
        public bool PreventsChatReports { get; set; } = false;
        public int? AcceptTextures { get; set; }
    }
}