using System.Diagnostics;
using System.Text.Json;
namespace OrangLauncher.Managers
{
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
        public string ReleaseUrl { get; set; } = "";
    }
    public static class UpdateManager
    {
        private const string GITHUB_OWNER = "Orang-Studio";
        private const string GITHUB_REPO = "OrangLaunch";
        private const string TAG_PREFIX = "Win-";
        private const string LEGACY_TAG_SUFFIX = "-Win";
        private static readonly Version CurrentVersion = GetBuildVersion();
        private static Version GetBuildVersion()
        {
            try
            {
                var v = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                if (v != null) return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
            }
            catch { }
            return new Version(0, 0, 0);
        }
        private static string? VersionFromWindowsTag(string tag)
        {
            if (tag.StartsWith(TAG_PREFIX, StringComparison.OrdinalIgnoreCase))
                return tag.Substring(TAG_PREFIX.Length);
            if (tag.EndsWith(LEGACY_TAG_SUFFIX, StringComparison.OrdinalIgnoreCase))
                return tag.Substring(0, tag.Length - LEGACY_TAG_SUFFIX.Length);
            return null;
        }
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
        private static Version ParseVersion(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString)) return new Version(0, 0, 0);
            var s = versionString.TrimStart('v', 'V');
            var parts = s.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
            int major = 0, minor = 0, patch = 0;
            if (parts.Length > 0) int.TryParse(parts[0], out major);
            if (parts.Length > 1) int.TryParse(parts[1], out minor);
            if (parts.Length > 2) int.TryParse(parts[2], out patch);
            return new Version(major, minor, patch);
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
                client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/" + CurrentVersion);
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                // the repo hosts win and linux releases so /releases/latest may point at a linux tag.
                var apiUrl = $"https://api.github.com/repos/{GITHUB_OWNER}/{GITHUB_REPO}/releases?per_page=30";
                var response = await client.GetStringAsync(apiUrl);
                using var doc = JsonDocument.Parse(response);
                JsonElement root = default;
                Version bestVersion = new Version(0, 0, 0);
                bool found = false;
                foreach (var rel in doc.RootElement.EnumerateArray())
                {
                    if (rel.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) continue;
                    if (rel.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                    var tag = rel.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
                    var verStr = VersionFromWindowsTag(tag);
                    if (verStr == null) continue;
                    var ver = ParseVersion(verStr);
                    if (!found || ver > bestVersion)
                    {
                        bestVersion = ver;
                        root = rel;
                        found = true;
                    }
                }
                if (!found) return result;
                var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                var releaseName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
                var body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                var htmlUrl = root.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? "" : "";
                result.NewVersion = bestVersion.ToString();
                result.ReleaseNotes = body;
                result.ReleaseName = releaseName;
                result.ReleaseUrl = htmlUrl;
                if (bestVersion > CurrentVersion)
                {
                    result.UpdateAvailable = true;
                }
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var assetName = asset.GetProperty("name").GetString() ?? "";
                        if (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                        {
                            result.DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            result.AssetName = assetName;
                            result.FileSize = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                            break;
                        }
                    }
                }
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
            if (!updateInfo.UpdateAvailable)
                return false;
            if (!IsInstalledBuild())
            {
                ReportProgress("Updates are disabled while running from a folder.");
                return false;
            }
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl) ||
                !updateInfo.AssetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                ReportProgress("No MSI installer found, opening the download page...");
                OpenReleasePage(updateInfo);
                return false;
            }
            try
            {
                ReportProgress("Downloading update...");
                ReportProgressPercent(0);
                var tempPath = Path.Combine(Path.GetTempPath(), updateInfo.AssetName);
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/" + CurrentVersion);
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
                    FileName = "msiexec.exe",
                    Arguments = $"/i \"{tempPath}\" /qb /norestart",
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
        private static bool? _isInstalled;
        public static bool IsInstalledBuild()
        {
            if (_isInstalled.HasValue) return _isInstalled.Value;
            _isInstalled = DetectInstalled();
            return _isInstalled.Value;
        }
        private static bool DetectInstalled()
        {
            try
            {
                var exeDir = Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(Environment.ProcessPath!) ?? "");
                if (string.IsNullOrEmpty(exeDir)) return false;
                if (File.Exists(Path.Combine(exeDir, "installed.txt"))) return true;
                foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry32, Microsoft.Win32.RegistryView.Registry64 })
                foreach (var hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
                {
                    using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
                    using var uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                    if (uninstall == null) continue;
                    foreach (var keyName in uninstall.GetSubKeyNames())
                    {
                        using var key = uninstall.OpenSubKey(keyName);
                        if (key == null) continue;
                        if (!string.Equals(key.GetValue("DisplayName") as string, "OrangLauncher", StringComparison.OrdinalIgnoreCase)) continue;
                        var loc = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(loc) && string.Equals(Path.TrimEndingDirectorySeparator(loc.TrimEnd('\\', '/')), exeDir, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
                {
                    if (string.Equals(Path.TrimEndingDirectorySeparator(Path.Combine(dir, "OrangLauncher")), exeDir, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }
        public static void OpenReleasePage(UpdateInfo? info)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = !string.IsNullOrEmpty(info?.ReleaseUrl)
                        ? info.ReleaseUrl
                        : $"https://github.com/{GITHUB_OWNER}/{GITHUB_REPO}/releases",
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
            return $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";
        }
        public static string GetAboutVersionText(string localizedLabel)
        {
            var label = System.Text.RegularExpressions.Regex
                .Replace(localizedLabel, @"\s*v?\d+(\.\d+)*\s*$", "").TrimEnd();
            return $"{label} {CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";
        }
    }
}