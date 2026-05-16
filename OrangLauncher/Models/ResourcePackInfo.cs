namespace OrangLauncher.Models
{
    public class ResourcePackInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Size { get; set; } = "";
        public string? ModrinthId { get; set; }
        public string? Sha512 { get; set; }
    }
}