namespace OrangLauncher.Backend
{
    public class MLaunchOption
    {
        public MSession? Session { get; set; }
        public int MaximumRamMb { get; set; } = 4096;
        public int MinimumRamMb { get; set; } = 512;
        public string? JavaPath { get; set; }
        public string? GameDirectory { get; set; }
        public string? GameLauncherName { get; set; }
        public string? GameLauncherVersion { get; set; }
        public string? VersionType { get; set; }
        public string? ServerIp { get; set; }
        public int? ServerPort { get; set; }
        public int? ScreenWidth { get; set; }
        public int? ScreenHeight { get; set; }
        public bool Fullscreen { get; set; }
        public MArgument[]? ExtraJvmArguments { get; set; }
        public MArgument[]? ExtraGameArguments { get; set; }
    }
}