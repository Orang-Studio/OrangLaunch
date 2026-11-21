namespace OrangLauncher.Models
{
    public class ModInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Size { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public double Opacity => IsEnabled ? 1.0 : 0.45;
        public string? ModrinthId { get; set; }
        public string? Sha512 { get; set; }
        public override string ToString() => string.IsNullOrEmpty(Size) ? Name : $"{Name}  ({Size})";
}   }