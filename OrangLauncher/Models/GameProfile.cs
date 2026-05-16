using System;
namespace OrangLauncher.Models
{
    public class GameProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Default";
        public string Version { get; set; } = "26.1";
        public string ModLoader { get; set; } = "vanilla";
        public string Ram { get; set; } = "4096";
        public string? GameDir { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
    }
}