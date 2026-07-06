namespace OrangLauncher.Models
{
    public class UserProfile
    {
        public string Uuid { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = "";
        public string Type { get; set; } = "Offline";
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? MinecraftToken { get; set; }
        public DateTime LastUsed { get; set; } = DateTime.Now;
        public string GetDisplayName()
        {
            return $"{Username} ({Type})";
        }
        public override string ToString()
        {
            return GetDisplayName();
        }
    }
}