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
        /// <summary>Point the telemetry endpoint at an unreachable host so events never upload.</summary>
        public bool DisableTelemetry { get; set; }
        /// <summary>Point the blocked-servers endpoint at an unreachable host so the client's server blacklist stays empty.</summary>
        public bool DisableServerBlacklist { get; set; }
    }
}