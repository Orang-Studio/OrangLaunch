using System.Text.Json;
using OrangLauncher.Backend;
namespace OrangLauncher.Managers
{
    public class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SyncProgress(Action<T> handler)
        {
            _handler = handler;
        }
        public void Report(T value)
        {
            _handler(value);
        }
    }
    public class ModLoaderVersion
    {
        public string Name { get; set; } = "";
        public string LoaderType { get; set; } = "";
        public string MinecraftVersion { get; set; } = "";
        public string LoaderVersion { get; set; } = "";
        public bool IsRecommended { get; set; }
    }
    public static class ModLoaderInstaller
    {
        public static event Action<string> OnProgressChanged = delegate { };
        public static event Action<int> OnProgressPercentageChanged = delegate { };
        private static readonly HttpClient _httpClient = new();
        private static void ReportProgress(string message) => OnProgressChanged?.Invoke(message);
        private static void ReportProgressPercent(int percent) => OnProgressPercentageChanged?.Invoke(percent);
        public static async Task<List<ModLoaderVersion>> GetAvailableLoadersAsync(string minecraftVersion)
        {
            var versions = new List<ModLoaderVersion>();
            try
            {
                var response = await _httpClient.GetStringAsync("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
                var json = JsonDocument.Parse(response);
                if (json.RootElement.TryGetProperty("promos", out var promos))
                {
                    var recommendedKey = $"{minecraftVersion}-recommended";
                    var latestKey = $"{minecraftVersion}-latest";
                    if (promos.TryGetProperty(recommendedKey, out var recommended))
                    {
                        var forgeVer = recommended.GetString() ?? "";
                        versions.Add(new ModLoaderVersion
                        {
                            Name = $"Forge {forgeVer} (Recommended)",
                            LoaderType = "forge",
                            MinecraftVersion = minecraftVersion,
                            LoaderVersion = forgeVer,
                            IsRecommended = true
                        });
                    }
                    if (promos.TryGetProperty(latestKey, out var latest))
                    {
                        var latestVer = latest.GetString() ?? "";
                        if (versions.Count == 0 || versions[0].LoaderVersion != latestVer)
                        {
                            versions.Add(new ModLoaderVersion
                            {
                                Name = $"Forge {latestVer}",
                                LoaderType = "forge",
                                MinecraftVersion = minecraftVersion,
                                LoaderVersion = latestVer,
                                IsRecommended = versions.Count == 0
                            });
                        }
                    }
                }
            }
            catch { }
            try
            {
                var response = await _httpClient.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}");
                var json = JsonDocument.Parse(response);
                var addedFabric = false;
                foreach (var item in json.RootElement.EnumerateArray().Take(5))
                {
                    if (addedFabric) break;
                    var loaderVersion = item.GetProperty("loader").GetProperty("version").GetString();
                    if (!string.IsNullOrEmpty(loaderVersion))
                    {
                        versions.Add(new ModLoaderVersion
                        {
                            Name = $"Fabric {loaderVersion}",
                            LoaderType = "fabric",
                            MinecraftVersion = minecraftVersion,
                            LoaderVersion = loaderVersion,
                            IsRecommended = versions.All(v => v.LoaderType != "fabric")
                        });
                        addedFabric = true;
                    }
                }
            }
            catch { }
            try
            {
                var response = await _httpClient.GetStringAsync($"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}");
                var json = JsonDocument.Parse(response);
                var addedQuilt = false;
                foreach (var item in json.RootElement.EnumerateArray().Take(5))
                {
                    if (addedQuilt) break;
                    var loaderVersion = item.GetProperty("loader").GetProperty("version").GetString();
                    if (!string.IsNullOrEmpty(loaderVersion))
                    {
                        versions.Add(new ModLoaderVersion
                        {
                            Name = $"Quilt {loaderVersion}",
                            LoaderType = "quilt",
                            MinecraftVersion = minecraftVersion,
                            LoaderVersion = loaderVersion,
                            IsRecommended = versions.All(v => v.LoaderType != "quilt")
                        });
                        addedQuilt = true;
                    }
                }
            }
            catch { }
            try
            {
                var neoforgeLoader = new NeoForgeVersionLoader(_httpClient);
                var neoforgeVersions = await neoforgeLoader.GetNeoForgeVersions(minecraftVersion);
                var latest = neoforgeVersions.FirstOrDefault();
                if (latest != null)
                {
                    versions.Add(new ModLoaderVersion
                    {
                        Name = $"NeoForge {latest.VersionName}",
                        LoaderType = "neoforge",
                        MinecraftVersion = minecraftVersion,
                        LoaderVersion = latest.VersionName,
                        IsRecommended = versions.All(v => v.LoaderType != "neoforge")
                    });
                }
            }
            catch { }
            return versions;
        }
        public static async Task<string> InstallLoaderAsync(string minecraftPath, string minecraftVersion, string loaderType, string? loaderVersion = null)
        {
            if (IsLoaderInstalled(minecraftPath, minecraftVersion, loaderType))
            {
                ReportProgress($"Using existing {loaderType} installation");
                return GetLatestInstalledLoaderVersion(minecraftPath, minecraftVersion, loaderType)!;
            }
            ReportProgress($"Installing {loaderType} for Minecraft {minecraftVersion}...");
            ReportProgressPercent(0);
            switch (loaderType.ToLower())
            {
                case "forge":
                    return await InstallForgeAsync(minecraftPath, minecraftVersion, loaderVersion);
                case "fabric":
                    return await InstallFabricAsync(minecraftPath, minecraftVersion, loaderVersion);
                case "quilt":
                    return await InstallQuiltAsync(minecraftPath, minecraftVersion, loaderVersion);
                case "neoforge":
                    return await InstallNeoForgeAsync(minecraftPath, minecraftVersion, loaderVersion);
                case "optifine":
                    return await InstallOptiFineAsync(minecraftPath, minecraftVersion, loaderVersion);
                default:
                    throw new ArgumentException($"Unknown loader type: {loaderType}");
            }
        }
        #region OptiFine Installation
        private static async Task<string> InstallOptiFineAsync(string minecraftPath, string mcVersion, string? edition)
        {
            ReportProgress("Installing OptiFine...");
            ReportProgressPercent(10);
            var path = new MinecraftPath(minecraftPath);
            var launcher = new MinecraftLauncher(path);
            EnsureLauncherProfilesExist(minecraftPath);
            if (!File.Exists(Path.Combine(path.Versions, mcVersion, $"{mcVersion}.jar")))
            {
                ReportProgress("Installing base Minecraft version...");
                ReportProgressPercent(15);
                var fileProgress = new SyncProgress<InstallerProgressChangedEventArgs>(e =>
                {
                    ReportProgress($"[Base] {e.Name}");
                    if (e.TotalTasks > 0)
                        ReportProgressPercent(15 + (int)(e.ProgressedTasks * 25.0 / e.TotalTasks));
                });
                await launcher.InstallAsync(mcVersion, fileProgress, new SyncProgress<ByteProgress>(e => { }));
            }
            ReportProgress("Fetching OptiFine builds...");
            ReportProgressPercent(45);
            var builds = await OptiFineClient.GetVersionsForAsync(mcVersion);
            var build = !string.IsNullOrEmpty(edition)
                ? builds.FirstOrDefault(b => b.Edition == edition || b.DisplayName == edition || b.FileName == edition) ?? builds.FirstOrDefault()
                : builds.FirstOrDefault();
            if (build == null)
                throw new Exception($"No OptiFine build available for Minecraft {mcVersion}");
            ReportProgress($"Downloading {build.DisplayName}...");
            var jar = await OptiFineClient.DownloadAsync(build, Path.Combine(Path.GetTempPath(), "OrangLauncher"),
                new SyncProgress<double>(p => ReportProgressPercent(45 + (int)(p * 35))));
            if (jar == null)
                throw new Exception("OptiFine download failed");
            ReportProgress("Running OptiFine installer...");
            ReportProgressPercent(85);
            var java = Backend.MinecraftLauncher.FindJava(17);
            var fullMcPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(minecraftPath));
            var redirectArg = "";
            var canRedirect = Path.GetFileName(fullMcPath).Equals(".minecraft", StringComparison.OrdinalIgnoreCase) &&
                              Path.GetDirectoryName(fullMcPath) is not null;
            if (canRedirect)
                redirectArg = $"\"-Duser.home={Path.GetDirectoryName(fullMcPath)}\" ";
            var psi = new System.Diagnostics.ProcessStartInfo(java, $"{redirectArg}-cp \"{jar}\" optifine.Installer")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (canRedirect)
            {
                psi.EnvironmentVariables["APPDATA"] = Path.GetDirectoryName(fullMcPath);
            }
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                if (proc == null)
                    throw new Exception("OptiFine installer failed to start");
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();
                if (proc.ExitCode != 0)
                {
                    var detail = (await stderrTask).Trim();
                    if (detail.Length == 0) detail = (await stdoutTask).Trim();
                    if (detail.Length > 400) detail = detail[..400];
                    throw new Exception($"OptiFine installer failed (exit {proc.ExitCode}){(detail.Length > 0 ? $": {detail}" : "")}");
                }
            }
            ReportProgressPercent(100);
            // the installer creates versions/<mc>-OptiFine_<edition>
            var expected = $"{mcVersion}-OptiFine_{build.Edition.Replace(' ', '_')}";
            var dir = Directory.Exists(path.Versions)
                ? Directory.GetDirectories(path.Versions)
                    .Select(Path.GetFileName)
                    .FirstOrDefault(d => d != null && d.Equals(expected, StringComparison.OrdinalIgnoreCase))
                  ?? Directory.GetDirectories(path.Versions)
                    .Select(Path.GetFileName)
                    .Where(d => d != null && d.Contains("OptiFine", StringComparison.OrdinalIgnoreCase) && d.StartsWith(mcVersion))
                    .OrderByDescending(d => Directory.GetLastWriteTimeUtc(Path.Combine(path.Versions, d!)))
                    .FirstOrDefault()
                : null;
            if (dir == null || !File.Exists(Path.Combine(path.Versions, dir, $"{dir}.json")))
                throw new Exception($"OptiFine version {expected} was not created in {path.Versions}");
            ReportProgress($"OptiFine installed: {dir}");
            return dir;
        }
        #endregion
        #region Forge Installation
        private static async Task<string> InstallForgeAsync(string minecraftPath, string mcVersion, string? forgeVersion)
        {
            ReportProgress("Installing Forge...");
            ReportProgressPercent(10);
            var path = new MinecraftPath(minecraftPath);
            var launcher = new MinecraftLauncher(path);
            EnsureLauncherProfilesExist(minecraftPath);
            if (!File.Exists(Path.Combine(path.Versions, mcVersion, $"{mcVersion}.jar")))
            {
                ReportProgress("Installing base Minecraft version...");
                ReportProgressPercent(15);
                var fileProgress = new SyncProgress<InstallerProgressChangedEventArgs>(e =>
                {
                    ReportProgress($"[Base] {e.Name}");
                    if (e.TotalTasks > 0)
                        ReportProgressPercent(15 + (int)(e.ProgressedTasks * 25.0 / e.TotalTasks));
                });
                var byteProgress = new SyncProgress<ByteProgress>(e => { });
                await launcher.InstallAsync(mcVersion, fileProgress, byteProgress);
            }
            else
            {
                ReportProgress("Base Minecraft version already installed");
                ReportProgressPercent(40);
            }
            ReportProgress("Installing Forge mod loader...");
            ReportProgressPercent(50);
            var forge = new ForgeInstaller(launcher);
            forge.OnProgress = msg => ReportProgress(msg);
            string versionName;
            if (!string.IsNullOrEmpty(forgeVersion))
            {
                versionName = await forge.Install(mcVersion, forgeVersion);
            }
            else
            {
                versionName = await forge.Install(mcVersion);
            }
            var versionJsonPath = path.GetVersionJsonPath(versionName);
            if (!File.Exists(versionJsonPath))
            {
                throw new Exception($"Forge installation failed: version JSON not found at {versionJsonPath}");
            }
            ReportProgress("Forge installation complete!");
            ReportProgressPercent(100);
            return versionName;
        }
        #endregion
        #region Fabric Installation
        private static async Task<string> InstallFabricAsync(string minecraftPath, string mcVersion, string? fabricVersion)
        {
            ReportProgress("Installing Fabric...");
            ReportProgressPercent(10);
            var path = new MinecraftPath(minecraftPath);
            var launcher = new MinecraftLauncher(path);
            if (!Directory.Exists(Path.Combine(path.Versions, mcVersion)))
            {
                ReportProgress("Installing base Minecraft version...");
                ReportProgressPercent(15);
                var fileProgress = new SyncProgress<InstallerProgressChangedEventArgs>(e =>
                {
                    ReportProgress($"[Base] {e.Name}");
                    if (e.TotalTasks > 0)
                        ReportProgressPercent(15 + (int)(e.ProgressedTasks * 25.0 / e.TotalTasks));
                });
                var byteProgress = new SyncProgress<ByteProgress>(e => { });
                await launcher.InstallAsync(mcVersion, fileProgress, byteProgress);
            }
            else
            {
                ReportProgress("Base Minecraft version already installed");
                ReportProgressPercent(40);
            }
            ReportProgress("Installing Fabric mod loader...");
            ReportProgressPercent(50);
            var fabric = new FabricInstaller(new HttpClient());
            string versionName;
            if (!string.IsNullOrEmpty(fabricVersion))
            {
                versionName = await fabric.Install(mcVersion, path, fabricVersion);
            }
            else
            {
                versionName = await fabric.Install(mcVersion, path);
            }
            var fabricJsonPath = path.GetVersionJsonPath(versionName);
            if (!File.Exists(fabricJsonPath))
            {
                throw new Exception($"Fabric installation failed: version JSON not found at {fabricJsonPath}");
            }
            ReportProgress("Fabric installation complete!");
            ReportProgressPercent(100);
            return versionName;
        }
        #endregion
        #region Quilt Installation
        private static async Task<string> InstallQuiltAsync(string minecraftPath, string mcVersion, string? quiltVersion)
        {
            ReportProgress("Installing Quilt...");
            ReportProgressPercent(10);
            var path = new MinecraftPath(minecraftPath);
            var launcher = new MinecraftLauncher(path);
            if (!Directory.Exists(Path.Combine(path.Versions, mcVersion)))
            {
                ReportProgress("Installing base Minecraft version...");
                ReportProgressPercent(15);
                var fileProgress = new SyncProgress<InstallerProgressChangedEventArgs>(e =>
                {
                    ReportProgress($"[Base] {e.Name}");
                    if (e.TotalTasks > 0)
                        ReportProgressPercent(15 + (int)(e.ProgressedTasks * 25.0 / e.TotalTasks));
                });
                var byteProgress = new SyncProgress<ByteProgress>(e => { });
                await launcher.InstallAsync(mcVersion, fileProgress, byteProgress);
            }
            else
            {
                ReportProgress("Base Minecraft version already installed");
                ReportProgressPercent(40);
            }
            ReportProgress("Installing Quilt mod loader...");
            ReportProgressPercent(50);
            var quilt = new QuiltInstaller(new HttpClient());
            string versionName;
            if (!string.IsNullOrEmpty(quiltVersion))
            {
                versionName = await quilt.Install(mcVersion, path, quiltVersion);
            }
            else
            {
                versionName = await quilt.Install(mcVersion, path);
            }
            var quiltJsonPath = path.GetVersionJsonPath(versionName);
            if (!File.Exists(quiltJsonPath))
            {
                throw new Exception($"Quilt installation failed: version JSON not found at {quiltJsonPath}");
            }
            ReportProgress("Quilt installation complete!");
            ReportProgressPercent(100);
            return versionName;
        }
        #endregion
        #region NeoForge Installation
        private static async Task<string> InstallNeoForgeAsync(string minecraftPath, string mcVersion, string? neoforgeVersion)
        {
            ReportProgress("Installing NeoForge...");
            ReportProgressPercent(10);
            var path = new MinecraftPath(minecraftPath);
            var launcher = new MinecraftLauncher(path);
            EnsureLauncherProfilesExist(minecraftPath);
            if (!Directory.Exists(Path.Combine(path.Versions, mcVersion)))
            {
                ReportProgress("Installing base Minecraft version...");
                ReportProgressPercent(15);
                var fileProgress = new SyncProgress<InstallerProgressChangedEventArgs>(e =>
                {
                    ReportProgress($"[Base] {e.Name}");
                    if (e.TotalTasks > 0)
                        ReportProgressPercent(15 + (int)(e.ProgressedTasks * 25.0 / e.TotalTasks));
                });
                var byteProgress = new SyncProgress<ByteProgress>(e => { });
                await launcher.InstallAsync(mcVersion, fileProgress, byteProgress);
            }
            else
            {
                ReportProgress("Base Minecraft version already installed");
                ReportProgressPercent(40);
            }
            ReportProgress("Installing NeoForge mod loader...");
            ReportProgressPercent(50);
            var neoforge = new NeoForgeInstaller(launcher);
            string versionName;
            if (!string.IsNullOrEmpty(neoforgeVersion))
            {
                versionName = await neoforge.Install(neoforgeVersion);
            }
            else
            {
                var versionLoader = new NeoForgeVersionLoader(new HttpClient());
                var allVersions = await versionLoader.GetNeoForgeVersions(mcVersion);
                var matching = allVersions.FirstOrDefault();
                if (matching == null)
                    throw new Exception($"No NeoForge versions found for Minecraft {mcVersion}");
                versionName = await neoforge.Install(matching.VersionName);
            }
            var versionJsonPath = path.GetVersionJsonPath(versionName);
            if (!File.Exists(versionJsonPath))
            {
                throw new Exception($"NeoForge installation failed: version JSON not found at {versionJsonPath}");
            }
            ReportProgress("NeoForge installation complete!");
            ReportProgressPercent(100);
            return versionName;
        }
        #endregion
        #region Helper Methods
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
            // NeoForge names its version dirs "neoforge-<prefix><build>" and we also have to check if it's not forge but neo...
            var neoPrefix = NeoForgeVersionLoader.GetVersionPrefix(mcVersion);
            if (neoPrefix != null &&
                dirName.StartsWith($"neoforge-{neoPrefix}", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
        public static bool IsLoaderInstalled(string minecraftPath, string mcVersion, string loaderType)
        {
            var versionsDir = Path.Combine(minecraftPath, "versions");
            if (!Directory.Exists(versionsDir)) return false;
            var loaderName = loaderType.ToLower();
            return Directory.GetDirectories(versionsDir)
                .Select(Path.GetFileName)
                .Any(name => name != null &&
                            VersionNameMatchesMcVersion(name, mcVersion) &&
                            MatchesLoaderType(name, loaderName) &&
                            File.Exists(Path.Combine(versionsDir, name, $"{name}.json")));
        }
        public static string? GetInstalledLoaderVersion(string minecraftPath, string mcVersion, string loaderType)
        {
            var versionsDir = Path.Combine(minecraftPath, "versions");
            if (!Directory.Exists(versionsDir)) return null;
            var loaderName = loaderType.ToLower();
            return Directory.GetDirectories(versionsDir)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => name != null &&
                                       VersionNameMatchesMcVersion(name, mcVersion) &&
                                       MatchesLoaderType(name, loaderName) &&
                                       File.Exists(Path.Combine(versionsDir, name, $"{name}.json")));
        }
        public static string? GetLatestInstalledLoaderVersion(string minecraftPath, string mcVersion, string loaderType)
        {
            var versionsDir = new DirectoryInfo(Path.Combine(minecraftPath, "versions"));
            if (!versionsDir.Exists) return null;
            var loaderName = loaderType.ToLower();
            var matchingDir = versionsDir.GetDirectories()
                .Where(d => MatchesLoaderType(d.Name, loaderName) &&
                            VersionNameMatchesMcVersion(d.Name, mcVersion) &&
                            File.Exists(Path.Combine(d.FullName, $"{d.Name}.json")))
                .OrderByDescending(d => d.LastWriteTime)
                .FirstOrDefault();
            return matchingDir?.Name;
        }
        private static void EnsureLauncherProfilesExist(string minecraftPath)
        {
            var profilesPath = Path.Combine(minecraftPath, "launcher_profiles.json");
            if (!File.Exists(profilesPath))
            {
                Directory.CreateDirectory(minecraftPath);
                File.WriteAllText(profilesPath, "{\"profiles\":{},\"selectedProfile\":null,\"authenticationDatabase\":{}}");
                System.Diagnostics.Debug.WriteLine($"[ModLoaderInstaller] Created launcher_profiles.json at {profilesPath}");
            }
        }
        private static bool MatchesLoaderType(string dirName, string loaderName)
        {
            var lower = dirName.ToLower();
            if (loaderName == "forge")
            {
                return lower.Contains("forge") && !lower.Contains("neoforge");
            }
            return lower.Contains(loaderName);
        }
        #endregion
    }
}