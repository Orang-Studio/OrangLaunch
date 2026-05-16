using System;
using System.IO;
namespace OrangLauncher.Models
{
    public class MinecraftInstance
    {
        public string InstanceId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Instance";
        public string Version { get; set; } = "26.1";
        public string ModLoader { get; set; } = "vanilla";
        public string Ram { get; set; } = "4096";
        public string JavaArgs { get; set; } = "";
        public string? JavaPath { get; set; }
        public string? Icon { get; set; }
        public string? InstalledVersionName { get; set; }
        public string PerformanceTier { get; set; } = "Auto";
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastPlayed { get; set; }
        public string BasePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OrangLauncher", "instances", InstanceId);
        public string MinecraftDir => Path.Combine(BasePath, ".minecraft");
        public string ModsDir => Path.Combine(MinecraftDir, "mods");
        public string ResourcePacksDir => Path.Combine(MinecraftDir, "resourcepacks");
        public string ShaderPacksDir => Path.Combine(MinecraftDir, "shaderpacks");
    }
}