using System.Diagnostics;
using System.Text.Json;
namespace OrangLauncher.Managers
{
    public class GithubRelease
    {
        public string TagName { get; set; } = "";
        public string Name { get; set; } = "";
        public string Body { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
        public DateTime PublishedAt { get; set; }
        public GithubAsset[] Assets { get; set; } = Array.Empty<GithubAsset>();
        public bool Prerelease { get; set; }
    }
    public class GithubAsset
    {
        public string Name { get; set; } = "";
        public string BrowserDownloadUrl { get; set; } = "";
        public long Size { get; set; }
    }
    public class UpdateInfo
    {
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "";
        public string NewVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string ReleaseName { get; set; } = "";
        public string AssetName { get; set; } = "";
        public long FileSize { get; set; }
    }
    public static class UpdateManager
    {
        private const string GITHUB_OWNER = "Orang-Studio";
        private const string GITHUB_REPO = "OrangLaunch";
        private static readonly Version CurrentVersion = new Version(5, 1);
        public static event Action<string>? ProgressChanged;
        public static event Action<int>? ProgressPercentChanged;
        private static void ReportProgress(string message)
        {
            ProgressChanged?.Invoke(message);
        }
        private static void ReportProgressPercent(int percent)
        {
            ProgressPercentChanged?.Invoke(percent);
        }
        private static Version ParseMajorMinor(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString)) return new Version(0, 0);
            var s = versionString.TrimStart('v', 'V');
            var parts = s.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            int major = 0, minor = 0;
            if (parts.Length > 0) int.TryParse(parts[0], out major);
            if (parts.Length > 1) int.TryParse(parts[1], out minor);
            return new Version(major, minor);
        }
        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var result = new UpdateInfo
            {
                CurrentVersion = GetCurrentVersionString(),
                UpdateAvailable = false
            };
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/6.0.0");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                var apiUrl = $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases/latest";
                var response = await client.GetStringAsync(apiUrl);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                var releaseName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
                var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                var htmlUrl = root.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? "" : "";
                var prerelease = root.TryGetProperty("prerelease", out var preProp) && preProp.GetBoolean();
                var newVersionStr = tagName.TrimStart('v', 'V');
                result.NewVersion = newVersionStr;
                result.ReleaseNotes = body;
                result.ReleaseName = releaseName;
                var newVersion = ParseMajorMinor(newVersionStr.Split('-')[0]);
                if (newVersion > CurrentVersion && !prerelease)
                {
                    result.UpdateAvailable = true;
                }
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var assetName = asset.GetProperty("name").GetString() ?? "";
                        if (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                            assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            assetName.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
                            assetName.Contains("installer", StringComparison.OrdinalIgnoreCase))
                        {
                            result.DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            result.AssetName = assetName;
                            result.FileSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(result.DownloadUrl)) result.DownloadUrl = htmlUrl;
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
                return result;
            }
        }
        public static async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
        {
            if (!updateInfo.UpdateAvailable || string.IsNullOrEmpty(updateInfo.DownloadUrl))
                return false;
            if (!string.IsNullOrEmpty(updateInfo.AssetName) &&
                (updateInfo.AssetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) || updateInfo.AssetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    ReportProgress("Downloading update...");
                    ReportProgressPercent(0);
                    var tempPath = Path.Combine(Path.GetTempPath(), updateInfo.AssetName);
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/6.0.0");
                    using var response = await client.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSize;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;
                        if (totalBytes > 0)
                        {
                            var percent = (int)(totalRead * 100 / totalBytes);
                            ReportProgressPercent(percent);
                            ReportProgress($"Downloading... {totalRead / 1024 / 1024:F1} MB / {totalBytes / 1024 / 1024:F1} MB");
                        }
                    }
                    ReportProgress("Download complete. Starting installer...");
                    ReportProgressPercent(100);
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = tempPath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                    return true;
                }
                catch (Exception ex)
                {
                    ReportProgress($"Download failed: {ex.Message}");
                    return false;
                }
            }
            else
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = updateInfo.DownloadUrl,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
        public static void OpenReleasesPage()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = $"https://github.com/{GITHUB_OWNER}/{GITHUB_REPO}/releases",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { }
        }
        public static void OpenGitHubPage()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = $"https://github.com/{GITHUB_OWNER}/{GITHUB_REPO}",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch { }
        }
        public static string GetCurrentVersionString()
        {
            return $"v{CurrentVersion.Major}.{CurrentVersion.Minor}";
        }
    }
}