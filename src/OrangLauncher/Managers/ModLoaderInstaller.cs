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
    public static class ModLoaderInstaller
    {
        public static event Action<string> OnProgressChanged = delegate { };
        public static event Action<int> OnProgressPercentageChanged = delegate { };
        private static readonly HttpClient _httpClient = new();
        private static void ReportProgress(string message) => OnProgressChanged?.Invoke(message);
        private static void ReportProgressPercent(int percent) => OnProgressPercentageChanged?.Invoke(percent);
        public static async Task<string> InstallLoaderAsync(string minecraftPath, string minecraftVersion, string loaderType, string? loaderVersion = null)
        {
            var existing = FindInstalledVersion(minecraftPath, loaderType, minecraftVersion, loaderVersion);
            if (existing != null)
            {
                ReportProgress($"Using existing {loaderType} installation {existing}");
                return existing;
            }
            ReportProgress($"Installing {loaderType} for Minecraft {minecraftVersion}...");
            ReportProgressPercent(0);
            if (loaderVersion is "N/A" or "Latest" or "") loaderVersion = null;
            if (loaderType.Equals("forge", StringComparison.OrdinalIgnoreCase) && loaderVersion != null && loaderVersion.StartsWith(minecraftVersion + "-"))
                loaderVersion = loaderVersion[(minecraftVersion.Length + 1)..];
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
        public static string NormalizeLoaderVersion(string loader, string mcVersion, string? loaderVersion)
        {
            var lv = (loaderVersion ?? "").Trim();
            if (lv is "N/A" or "Latest") return "";
            if (loader == "forge" && lv.StartsWith(mcVersion + "-")) lv = lv[(mcVersion.Length + 1)..];
            if (loader == "neoforge" && lv.StartsWith(mcVersion + "-")) lv = lv[(mcVersion.Length + 1)..];
            if (loader == "optifine")
            {
                var m = System.Text.RegularExpressions.Regex.Match(lv, @"^(?:preview_)?OptiFine_[0-9.]+_(.+?)(?:\.jar)?$");
                if (m.Success) lv = m.Groups[1].Value;
            }
            return lv;
        }
        public static string? FindInstalledVersion(string minecraftPath, string loaderType, string mcVersion, string? loaderVersion)
        {
            var versionsDir = new DirectoryInfo(Path.Combine(minecraftPath, "versions"));
            if (!versionsDir.Exists) return null;
            var loader = loaderType.ToLowerInvariant();
            var lv = NormalizeLoaderVersion(loader, mcVersion, loaderVersion);
            var mc = mcVersion.ToLowerInvariant();
            var candidates = new List<DirectoryInfo>();
            foreach (var d in versionsDir.GetDirectories())
            {
                if (!File.Exists(Path.Combine(d.FullName, $"{d.Name}.json"))) continue;
                var low = d.Name.ToLowerInvariant();
                bool match = loader switch
                {
                    "fabric" => low.StartsWith("fabric-loader-") && low.EndsWith("-" + mc),
                    "quilt" => low.StartsWith("quilt-loader-") && low.EndsWith("-" + mc),
                    "forge" => (low.StartsWith(mc + "-forge") || low.StartsWith("forge-" + mc)) && !low.Contains("neoforge"),
                    "neoforge" => (low.StartsWith("neoforge-") && VersionNameMatchesMcVersion(d.Name, mcVersion)) || (mc == NeoForgeVersionLoader.LegacyMcVersion && low.StartsWith(mc + "-forge-")),
                    "optifine" => low.StartsWith(mc + "-optifine"),
                    _ => false
                };
                if (!match) continue;
                if (!string.IsNullOrEmpty(lv) && !d.Name.Contains(lv, StringComparison.OrdinalIgnoreCase)) continue;
                candidates.Add(d);
            }
            return candidates.OrderByDescending(d => d.LastWriteTimeUtc).FirstOrDefault()?.Name;
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
        #endregion
    }
}