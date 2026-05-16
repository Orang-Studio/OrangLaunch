using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading.Tasks;
using OrangLauncher.Backend;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public static class SystemPerformanceHelper
    {
        public static string DetectPerformanceTier()
        {
            try
            {
                long totalRamMB = GetTotalRamMB();
                int cpuCores = Environment.ProcessorCount;
                if (totalRamMB >= 16384 && cpuCores >= 8)
                    return "High";
                if (totalRamMB < 8192 || cpuCores < 4)
                    return "Low";
                return "Mid";
            }
            catch
            {
                return "Mid";
            }
        }
        public static long GetTotalRamMB()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var totalKB = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                    return totalKB / 1024;
                }
            }
            catch { }
            return 8192; 
        }
        public static List<string> GetPerformanceArgumentStrings(string tier, int allocatedRamMB)
        {
            var args = new List<string>();
            string effectiveTier = tier;
            if (effectiveTier.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            {
                effectiveTier = DetectPerformanceTier();
            }
            switch (effectiveTier.ToLower())
            {
                case "low":
                    args.Add("-XX:+UseG1GC");
                    args.Add("-XX:+ParallelRefProcEnabled");
                    args.Add("-XX:MaxGCPauseMillis=200");
                    args.Add("-XX:+UnlockExperimentalVMOptions");
                    args.Add("-XX:G1HeapRegionSize=16M");
                    break;
                case "mid":
                    args.Add("-XX:+UseG1GC");
                    args.Add("-XX:+ParallelRefProcEnabled");
                    args.Add("-XX:MaxGCPauseMillis=100");
                    args.Add("-XX:+UnlockExperimentalVMOptions");
                    args.Add("-XX:+DisableExplicitGC");
                    args.Add("-XX:G1NewSizePercent=20");
                    args.Add("-XX:G1ReservePercent=20");
                    args.Add("-XX:G1HeapRegionSize=32M");
                    break;
                case "high":
                    args.Add("-XX:+UseG1GC");
                    args.Add("-XX:+ParallelRefProcEnabled");
                    args.Add("-XX:MaxGCPauseMillis=50");
                    args.Add("-XX:+UnlockExperimentalVMOptions");
                    args.Add("-XX:+DisableExplicitGC");
                    args.Add("-XX:+AlwaysPreTouch");
                    args.Add("-XX:G1NewSizePercent=30");
                    args.Add("-XX:G1ReservePercent=20");
                    args.Add("-XX:G1HeapRegionSize=32M");
                    args.Add("-XX:G1MixedGCCountTarget=4");
                    args.Add("-XX:InitiatingHeapOccupancyPercent=15");
                    args.Add("-XX:G1MixedGCLiveThresholdPercent=90");
                    args.Add("-XX:G1RSetUpdatingPauseTimePercent=5");
                    args.Add("-XX:SurvivorRatio=32");
                    args.Add("-XX:+PerfDisableSharedMem");
                    args.Add("-XX:MaxTenuringThreshold=1");
                    break;
            }
            return args;
        }
        public static List<MArgument> GetPerformanceArguments(string tier, int allocatedRamMB)
        {
            var stringArgs = GetPerformanceArgumentStrings(tier, allocatedRamMB);
            return stringArgs.Select(arg => new MArgument(arg)).ToList();
        }
    }
    public class GameProfileManager
    {
        private static GameProfileManager? _instance;
        public static GameProfileManager Instance => _instance ??= new GameProfileManager();
        private static bool VersionNameMatchesMcVersion(string dirName, string mcVersion)
        {
            if (dirName.EndsWith($"-{mcVersion}", StringComparison.Ordinal))
                return true;
            if (dirName.StartsWith($"{mcVersion}-", StringComparison.Ordinal))
                return true;
            if (dirName.Contains($"-{mcVersion}-", StringComparison.Ordinal))
                return true;
            if (dirName.Equals(mcVersion, StringComparison.Ordinal))
                return true;
            var mcParts = mcVersion.Split('.');
            if (mcParts.Length >= 2)
            {
                string neoPrefix = mcParts.Length >= 3
                    ? $"{mcParts[1]}.{mcParts[2]}."
                    : $"{mcParts[1]}.0.";
                if (dirName.StartsWith($"neoforge-{neoPrefix}", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        private List<GameProfile> _profiles = new();
        private string? _selectedId;
        private readonly string _profilesPath;
        private GameProfileManager()
        {
            _profilesPath = Path.Combine(PlatformPaths.GetDataDir(), "game_profiles.json");
            LoadProfiles();
        }
        private void LoadProfiles()
        {
            try
            {
                if (File.Exists(_profilesPath))
                {
                    var json = File.ReadAllText(_profilesPath);
                    var data = JsonSerializer.Deserialize<ProfilesData>(json);
                    if (data != null)
                    {
                        _profiles = data.Profiles ?? new List<GameProfile>();
                        _selectedId = data.SelectedId;
                    }
                }
            }
            catch { }
        }
        public List<GameProfile> GetProfiles() => _profiles.ToList();
        public GameProfile? GetSelectedProfile()
        {
            return _profiles.FirstOrDefault(p => p.Id == _selectedId) ?? _profiles.FirstOrDefault();
        }
        public void SetSelectedProfile(string id)
        {
            _selectedId = id;
        }
        public string GetModsDirectory(string profileId)
        {
            var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile?.GameDir != null)
                return Path.Combine(profile.GameDir, "mods");
            return Path.Combine(PlatformPaths.GetMinecraftDir(), "mods");
        }
        public async Task<List<VersionInfo>> GetVersions()
        {
            var versions = new List<VersionInfo>();
            try
            {
                var launcher = new MinecraftLauncher();
                var versionList = await launcher.GetAllVersionsAsync();
                foreach (var v in versionList)
                {
                    versions.Add(new VersionInfo { Id = v.Id, Type = v.Type ?? "release" });
                }
            }
            catch { }
            return versions;
        }
        public async Task<Process> LaunchGame(string version, int maxRam, string javaArgs, MSession session, string modLoader, string? gameDir, string? serverIp = null, int? serverPort = null, string performanceTier = "Auto", bool useDiscreteGpu = false, string? javaPath = null)
        {
            var minecraftPath = new MinecraftPath(PlatformPaths.GetMinecraftDir());
            var launcher = new MinecraftLauncher(minecraftPath);
            string versionToLaunch = version;
            if (!string.IsNullOrEmpty(modLoader) && modLoader.ToLower() != "vanilla" && modLoader.ToLower() != "none")
            {
                bool versionAlreadyModded = version.ToLower().Contains("fabric") || 
                                           version.ToLower().Contains("forge") || 
                                           version.ToLower().Contains("quilt") || 
                                           version.ToLower().Contains("neoforge") ||
                                           version.ToLower().Contains("liteloader");
                if (!versionAlreadyModded)
                {
                    var versionsDir = minecraftPath.Versions;
                    if (Directory.Exists(versionsDir))
                    {
                        var dirs = Directory.GetDirectories(versionsDir);
                        foreach (var dir in dirs)
                        {
                            var dirName = Path.GetFileName(dir);
                            if (dirName != null && VersionNameMatchesMcVersion(dirName, version) && dirName.ToLower().Contains(modLoader.ToLower()))
                            {
                                var versionJsonPath = Path.Combine(dir, $"{dirName}.json");
                                if (File.Exists(versionJsonPath))
                                {
                                    versionToLaunch = dirName;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            var launchOption = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = maxRam,
                MinimumRamMb = Math.Min(1024, maxRam / 2),
                GameLauncherName = "OrangLauncher",
                GameLauncherVersion = "1.0",
                VersionType = "OrangLauncher",
                GameDirectory = gameDir,
                JavaPath = javaPath
            };
            var jvmArgs = new List<MArgument>();
            if (!string.IsNullOrEmpty(javaArgs))
            {
                var args = javaArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var arg in args)
                {
                    jvmArgs.Add(new MArgument(arg));
                }
            }
            else
            {
                jvmArgs = SystemPerformanceHelper.GetPerformanceArguments(performanceTier, maxRam);
            }
            if (jvmArgs.Count > 0)
                launchOption.ExtraJvmArguments = jvmArgs.ToArray();
            var gameArgs = new List<MArgument>();
            if (!string.IsNullOrEmpty(serverIp))
            {
                string serverString = serverIp;
                if (serverPort.HasValue && serverPort.Value != 25565)
                {
                    serverString = $"{serverIp}:{serverPort.Value}";
                }
                gameArgs.Add(new MArgument("--quickPlayMultiplayer"));
                gameArgs.Add(new MArgument(serverString));
                gameArgs.Add(new MArgument("--server"));
                gameArgs.Add(new MArgument(serverIp));
                if (serverPort.HasValue)
                {
                    gameArgs.Add(new MArgument("--port"));
                    gameArgs.Add(new MArgument(serverPort.Value.ToString()));
                }
            }
            if (gameArgs.Count > 0)
                launchOption.ExtraGameArguments = gameArgs.ToArray();
            var process = await launcher.InstallAndBuildProcessAsync(versionToLaunch, launchOption);
            if (useDiscreteGpu)
            {
                process.StartInfo.EnvironmentVariables["SHIM_MCCOMPAT"] = "0x800000001";
                process.StartInfo.EnvironmentVariables["AmdPowerXpressRequestHighPerformance"] = "1";
                process.StartInfo.EnvironmentVariables["__NV_PRIME_RENDER_OFFLOAD"] = "1";
                process.StartInfo.EnvironmentVariables["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia";
                System.Diagnostics.Debug.WriteLine("Discrete GPU mode enabled - set environment variables");
            }
            System.Diagnostics.Debug.WriteLine("===== MINECRAFT LAUNCH INFORMATION =====");
            System.Diagnostics.Debug.WriteLine($"Version: {versionToLaunch}");
            System.Diagnostics.Debug.WriteLine($"Username: {session.Username}");
            System.Diagnostics.Debug.WriteLine($"RAM: {launchOption.MinimumRamMb}MB - {launchOption.MaximumRamMb}MB");
            System.Diagnostics.Debug.WriteLine($"Game Directory: {minecraftPath.BasePath}");
            System.Diagnostics.Debug.WriteLine($"Performance Tier: {performanceTier}");
            System.Diagnostics.Debug.WriteLine($"Use Discrete GPU: {useDiscreteGpu}");
            if (!string.IsNullOrEmpty(serverIp))
                System.Diagnostics.Debug.WriteLine($"Quick Play Server: {serverIp}:{serverPort ?? 25565}");
            System.Diagnostics.Debug.WriteLine($"JVM Arguments ({jvmArgs.Count}):");
            foreach (var arg in jvmArgs)
            {
                System.Diagnostics.Debug.WriteLine($"  {arg}");
            }
            System.Diagnostics.Debug.WriteLine($"Game Arguments ({gameArgs.Count}):");
            foreach (var arg in gameArgs)
            {
                System.Diagnostics.Debug.WriteLine($"  {arg}");
            }
            System.Diagnostics.Debug.WriteLine($"Launch Command: {process.StartInfo.FileName}");
            System.Diagnostics.Debug.WriteLine($"Arguments: {process.StartInfo.Arguments}");
            System.Diagnostics.Debug.WriteLine("========================================");
            return process;
        }
        public class VersionInfo
        {
            public string Id { get; set; } = "";
            public string Type { get; set; } = "";
        }
        private class ProfilesData
        {
            public List<GameProfile>? Profiles { get; set; }
            public string? SelectedId { get; set; }
        }
    }
}