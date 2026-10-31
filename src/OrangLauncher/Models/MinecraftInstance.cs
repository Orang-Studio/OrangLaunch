namespace OrangLauncher.Models
{
    public class InstanceOptions
    {
        public string JvmArgs { get; set; } = "";
        public int ResWidth { get; set; }
        public int ResHeight { get; set; }
        public bool Fullscreen { get; set; }
        public bool FullscreenManaged { get; set; }
        public bool ResManaged { get; set; }
        public int MinRamMb { get; set; }
        public int PermGenMb { get; set; }
        public string PreLaunchCmd { get; set; } = "";
        public string WrapperCmd { get; set; } = "";
        public string PostExitCmd { get; set; } = "";
        public bool LegacyFixes { get; set; }
        public bool AntiBan { get; set; } = true;
        public bool Demo { get; set; }
        public bool DiscreteGpu { get; set; }
        public InstanceOptions Clone() => (InstanceOptions)MemberwiseClone();
    }
    public class MinecraftInstance
    {
        public string InstanceId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "New Instance";
        public string Version { get; set; } = "1.20.1";
        public string ModLoader { get; set; } = "vanilla";
        public string LoaderVersion { get; set; } = "";
        public string Ram { get; set; } = "4096";
        public string JavaArgs { get; set; } = "";
        public string? JavaPath { get; set; }
        public string? Icon { get; set; }
        public string PerformanceTier { get; set; } = "Auto";
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastPlayed { get; set; }
        public long PlayTimeSeconds { get; set; }
        public string? InstalledVersionName { get; set; }
        public string EnvVars { get; set; } = "";
        public InstanceOptions Options { get; set; } = new();
        public string BasePath => Path.Combine(Managers.PlatformPaths.GetInstancesDir(), InstanceId);
        public string MinecraftDir => Path.Combine(BasePath, ".minecraft");
        public string ModsDir => Path.Combine(MinecraftDir, "mods");
        public string SavesDir => Path.Combine(MinecraftDir, "saves");
        public string ResourcePacksDir => Path.Combine(MinecraftDir, "resourcepacks");
        public string ShaderPacksDir => Path.Combine(MinecraftDir, "shaderpacks");
        public string ScreenshotsDir => Path.Combine(MinecraftDir, "screenshots");
        public string LoaderKey => string.IsNullOrWhiteSpace(ModLoader) || ModLoader.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? "vanilla" : ModLoader.Trim().ToLowerInvariant();
        public bool IsVanilla => LoaderKey == "vanilla";
        public int RamMb
        {
            get
            {
                var digits = new string((Ram ?? "").Where(char.IsDigit).ToArray());
                if (!int.TryParse(digits, out var val) || val <= 0) return 4096;
                var upper = (Ram ?? "").ToUpperInvariant();
                if (upper.Contains('G') || val < 64) return val * 1024;
                return val;
            }
            set => Ram = Math.Max(256, value).ToString();
        }
    }
}