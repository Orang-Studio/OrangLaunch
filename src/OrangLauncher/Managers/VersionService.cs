using System.Text.Json;
using System.Xml.Linq;
using OrangLauncher.Backend;
namespace OrangLauncher.Managers
{
    public static class SystemPerformanceHelper
    {
        public static string DetectPerformanceTier()
        {
            try
            {
                int totalRamGB = GetTotalRamGB();
                int cpuCores = Environment.ProcessorCount;
                if (totalRamGB >= 16 && cpuCores >= 8)
                    return "High";
                if (totalRamGB < 8 || cpuCores < 4)
                    return "Low";
                return "Mid";
            }
            catch
            {
                return "Mid";
            }
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
        private static long _totalRam, _visibleRam;
        public static long GetTotalRamMB()
        {
            if (_totalRam > 0) return _totalRam;
            try
            {
                if (GetPhysicallyInstalledSystemMemory(out var kb) && kb > 0) return _totalRam = kb / 1024;
            }
            catch { }
            return _totalRam = GetVisibleRamMB();
        }
        private static long GetVisibleRamMB()
        {
            if (_visibleRam > 0) return _visibleRam;
            try
            {
                var st = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref st) && st.ullTotalPhys > 0)
                    return _visibleRam = (long)(st.ullTotalPhys / (1024 * 1024));
            }
            catch { }
            return 8192;
        }
        public static int GetTotalRamGB() => (int)Math.Round(GetTotalRamMB() / 1024.0);
        public static int GetMaxGameRamMB() => Math.Max(2048, (GetTotalRamGB() - 1) * 1024);
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
                    args.Add("-XX:+PerfDisableSharedMem");
                    args.Add("-XX:MaxTenuringThreshold=1");
                    break;
            }
            return args;
        }
    }
    public class GameVersionEntry
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "release";
        public override string ToString() => Id;
    }
    public static class VersionService
    {
        private static List<GameVersionEntry>? _cache;
        private static DateTime _fetched;
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static string CachePath => Path.Combine(PlatformPaths.GetDataDir(), "version_cache.json");
        public static List<GameVersionEntry> Cached
        {
            get
            {
                if (_cache != null) return _cache;
                try
                {
                    if (File.Exists(CachePath))
                        _cache = JsonSerializer.Deserialize<List<GameVersionEntry>>(File.ReadAllText(CachePath));
                }
                catch { }
                return _cache ?? new List<GameVersionEntry>();
            }
        }
        public static async Task<List<GameVersionEntry>> GetVersionsAsync(bool force = false)
        {
            await Gate.WaitAsync();
            try
            {
                if (!force && _cache != null && _cache.Count > 0 && DateTime.UtcNow - _fetched < TimeSpan.FromMinutes(30))
                    return _cache;
                try
                {
                    var list = await new MinecraftLauncher(new MinecraftPath(PlatformPaths.GetMinecraftDir())).GetAllVersionsAsync();
                    _cache = list.Select(v => new GameVersionEntry { Id = v.Id, Type = v.Type ?? "release" }).ToList();
                    _fetched = DateTime.UtcNow;
                    try { File.WriteAllText(CachePath, JsonSerializer.Serialize(_cache)); } catch { }
                }
                catch
                {
                    _cache = Cached;
                }
                return _cache;
            }
            finally
            {
                Gate.Release();
            }
        }
        public static bool IsKnown(string version)
        {
            var list = Cached;
            return list.Count == 0 || list.Any(v => v.Id == version);
        }
        public static List<string> Suggestions(string version)
        {
            if (string.IsNullOrEmpty(version)) return new List<string>();
            var prefix = version.Length >= 4 ? version[..4] : version;
            return Cached.Where(v => v.Id.Contains(prefix)).Take(5).Select(v => v.Id).ToList();
        }
    }
    public static class LoaderVersionService
    {
        public static readonly string[] Loaders = ["vanilla", "forge", "neoforge", "fabric", "quilt", "optifine"];
        private static readonly HttpClient Http = CreateClient();
        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher");
            return c;
        }
        private static List<string>? _forgeAll;
        public static async Task<List<string>> FetchAsync(string loader, string mcVersion)
        {
            if (string.IsNullOrWhiteSpace(mcVersion)) return new List<string>();
            switch ((loader ?? "").ToLowerInvariant())
            {
                case "forge":
                    {
                        if (_forgeAll == null)
                        {
                            var xml = await Http.GetStringAsync("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml");
                            _forgeAll = XDocument.Parse(xml).Descendants("version").Select(e => e.Value).ToList();
                        }
                        var prefix = mcVersion + "-";
                        return _forgeAll.Where(v => v.StartsWith(prefix, StringComparison.Ordinal))
                            .Select(v => v[prefix.Length..])
                            .OrderByDescending(v => v, Comparer<string>.Create(CompareNumeric))
                            .ToList();
                    }
                case "neoforge":
                    {
                        var all = await new NeoForgeVersionLoader(Http).GetNeoForgeVersions(mcVersion);
                        var names = all.Select(v => v.VersionName).ToList();
                        var stable = names.Where(n => !n.Contains("beta") && !n.Contains("alpha")).ToList();
                        return stable.Count > 0 ? stable : names;
                    }
                case "fabric":
                    return await new FabricInstaller(Http).GetLoaderVersionsAsync(mcVersion);
                case "quilt":
                    {
                        var all = await new QuiltInstaller(Http).GetLoaderVersionsAsync(mcVersion);
                        var stable = all.Where(n => !n.Contains("beta") && !n.Contains("alpha") && !n.Contains("pre") && !n.Contains("rc")).ToList();
                        return stable.Count > 0 ? stable.Concat(all.Except(stable)).ToList() : all;
                    }
                case "optifine":
                    return (await OptiFineClient.GetVersionsForAsync(mcVersion)).Select(v => v.FileName).ToList();
                default:
                    return new List<string>();
            }
        }
        private static int CompareNumeric(string a, string b)
        {
            var na = System.Text.RegularExpressions.Regex.Matches(a, @"\d+").Select(m => long.Parse(m.Value)).ToList();
            var nb = System.Text.RegularExpressions.Regex.Matches(b, @"\d+").Select(m => long.Parse(m.Value)).ToList();
            for (int i = 0; i < Math.Max(na.Count, nb.Count); i++)
            {
                var x = i < na.Count ? na[i] : -1;
                var y = i < nb.Count ? nb[i] : -1;
                if (x != y) return x.CompareTo(y);
            }
            return string.CompareOrdinal(a, b);
        }
    }
}
