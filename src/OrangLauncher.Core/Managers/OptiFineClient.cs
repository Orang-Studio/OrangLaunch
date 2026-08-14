using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OrangLauncher.Managers
{
    public class OptiFineVersion
    {
        public string FileName { get; set; } = "";
        //Minecraft version the build targets, e.g. "1.21.11"
        public string McVersion { get; set; } = "";
        public string Edition { get; set; } = "";
        public bool IsPreview { get; set; }
        public string DisplayName => $"OptiFine {McVersion} {Edition}{(IsPreview ? " (preview)" : "")}";
    }

    // optifine.net has no API so this scrapes the
    // downloads page for builds and gives the tokenized download link
    // from the ad page adloadx?f=X > downloadx?f=X&x=TOKEN.

    public static class OptiFineClient
    {
        private const string DownloadsUrl = "https://optifine.net/downloads";
        private const string SiteBase = "https://optifine.net/";
        private static readonly HttpClient Http = CreateClient();
        private static List<OptiFineVersion>? _cache;
        private static DateTime _cacheTime;

        private static HttpClient CreateClient()
        {
            var c = new HttpClient();
            c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) OrangLauncher");
            return c;
        }

        private static readonly Regex FileRegex = new(@"adloadx\?f=([^""'&]+\.jar)", RegexOptions.Compiled);
        private static readonly Regex NameRegex = new(@"^(preview_)?OptiFine_([0-9][0-9.]*?)_(.+)\.jar$", RegexOptions.Compiled);
        private static readonly Regex TokenRegex = new(@"downloadx\?f=[^""'<> ]+", RegexOptions.Compiled);

        // All OptiFine builds listed on optifine.net, newest first
        public static async Task<List<OptiFineVersion>> ListVersionsAsync(bool includePreviews = true)
        {
            if (_cache == null || DateTime.UtcNow - _cacheTime > TimeSpan.FromMinutes(30))
            {
                var html = await Http.GetStringAsync(DownloadsUrl);
                var list = new List<OptiFineVersion>();
                var seen = new HashSet<string>();
                foreach (Match m in FileRegex.Matches(html))
                {
                    var file = m.Groups[1].Value;
                    if (!seen.Add(file)) continue;
                    var nm = NameRegex.Match(file);
                    if (!nm.Success) continue;
                    list.Add(new OptiFineVersion
                    {
                        FileName = file,
                        IsPreview = nm.Groups[1].Success,
                        McVersion = nm.Groups[2].Value,
                        Edition = nm.Groups[3].Value.Replace('_', ' ')
                    });
                }
                _cache = list;
                _cacheTime = DateTime.UtcNow;
            }
            return includePreviews ? _cache.ToList() : _cache.Where(v => !v.IsPreview).ToList();
        }
        public static async Task<List<OptiFineVersion>> GetVersionsForAsync(string mcVersion, bool includePreviews = true)
        {
            var all = await ListVersionsAsync(includePreviews);
            return all.Where(v => v.McVersion == mcVersion).ToList();
        }

        // Resolves the real tokenized download
        public static async Task<string?> GetDownloadUrlAsync(string fileName)
        {
            var html = await Http.GetStringAsync($"{SiteBase}adloadx?f={Uri.EscapeDataString(fileName)}");
            var m = TokenRegex.Match(html);
            return m.Success ? SiteBase + m.Value.Replace("&amp;", "&") : null;
        }

        // download a build to destDir; returns the jar path. All rights to optifine team
        public static async Task<string?> DownloadAsync(OptiFineVersion version, string destDir, IProgress<double>? progress = null)
        {
            var url = await GetDownloadUrlAsync(version.FileName);
            if (url == null) return null;
            Directory.CreateDirectory(destDir);
            var dest = Path.Combine(destDir, version.FileName);
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            await using var src = await response.Content.ReadAsStreamAsync();
            await using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;
                if (total > 0) progress?.Report((double)readTotal / total);
            }
            return dest;
        }

        public static bool RunInstaller(string jarPath, string? minecraftPath = null)
        {
            try
            {
                var java = Backend.MinecraftLauncher.FindJava(17);
                var javaw = Path.Combine(Path.GetDirectoryName(java) ?? "", "javaw.exe");
                if (File.Exists(javaw)) java = javaw;
                var psi = new ProcessStartInfo(java, $"-jar \"{jarPath}\"") { UseShellExecute = false };
                if (minecraftPath != null)
                {
                    var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(minecraftPath));
                    if (Path.GetFileName(full).Equals(".minecraft", StringComparison.OrdinalIgnoreCase) &&
                        Path.GetDirectoryName(full) is string parent)
                        psi.EnvironmentVariables["APPDATA"] = parent;
                }
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
}   }   }   }