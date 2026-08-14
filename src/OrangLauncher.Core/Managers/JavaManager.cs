using System.Diagnostics;
using System.IO.Compression;
namespace OrangLauncher.Managers
{
    public enum JavaArchitecture { X86, X64 }
    public class JavaInstallation
    {
        public int MajorVersion { get; set; }
        public string Path { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsManaged { get; set; }
    }
    public static class JavaManager
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };
        public static event Action<string>? OnProgressChanged;
        public static event Action<int>? OnProgressPercentChanged;
        private static void ReportProgress(string msg) => OnProgressChanged?.Invoke(msg);
        private static void ReportPercent(int pct) => OnProgressPercentChanged?.Invoke(pct);
        private static readonly Dictionary<(int Major, JavaArchitecture Arch), string> AdoptiumUrls = new()
        {
            [(8, JavaArchitecture.X86)] = "https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u472-b08/OpenJDK8U-jre_x86-32_windows_hotspot_8u472b08.zip",
            [(8, JavaArchitecture.X64)] = "https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u472-b08/OpenJDK8U-jre_x64_windows_hotspot_8u472b08.zip",
            [(17, JavaArchitecture.X86)] = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.17%2B10/OpenJDK17U-jre_x86-32_windows_hotspot_17.0.17_10.zip",
            [(17, JavaArchitecture.X64)] = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.17%2B10/OpenJDK17U-jre_x64_windows_hotspot_17.0.17_10.zip",
            [(21, JavaArchitecture.X64)] = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.10%2B7/OpenJDK21U-jre_x64_windows_hotspot_21.0.10_7.zip",
            [(25, JavaArchitecture.X64)] = "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.2%2B10/OpenJDK25U-jre_x64_windows_hotspot_25.0.2_10.zip",
        };

        public static JavaArchitecture PreferredArchitecture
        {
            get { LoadPreferenceIfNeeded(); return _preferredArchitecture; }
            set { _preferredArchitecture = value; SavePreference(); }
        }
        private static JavaArchitecture _preferredArchitecture = JavaArchitecture.X64;
        private static bool _preferenceLoaded;
        private static string PreferenceFilePath => System.IO.Path.Combine(PlatformPaths.GetDataDir(), "java_architecture.txt");
        private static void LoadPreferenceIfNeeded()
        {
            if (_preferenceLoaded) return;
            _preferenceLoaded = true;
            try
            {
                if (File.Exists(PreferenceFilePath) &&
                    Enum.TryParse<JavaArchitecture>(File.ReadAllText(PreferenceFilePath).Trim(), true, out var arch))
                    _preferredArchitecture = arch;
            }
            catch { }
        }
        private static void SavePreference()
        {
            try { File.WriteAllText(PreferenceFilePath, _preferredArchitecture.ToString()); }
            catch { }
        }
        public static List<JavaArchitecture> GetAvailableArchitectures(int majorVersion)
            => Enum.GetValues<JavaArchitecture>().Where(a => AdoptiumUrls.ContainsKey((majorVersion, a))).ToList();
        public static JavaArchitecture ResolveArchitecture(int majorVersion, JavaArchitecture? requested = null)
        {
            var want = requested ?? PreferredArchitecture;
            var available = GetAvailableArchitectures(majorVersion);
            if (available.Contains(want)) return want;
            return available.Contains(JavaArchitecture.X64) ? JavaArchitecture.X64 : available.FirstOrDefault();
        }

        public static string GetJavaInstallDir()
        {
            var dir = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "java");
            Directory.CreateDirectory(dir);
            return dir;
        }
        private static string ArchSuffix(JavaArchitecture arch) => arch == JavaArchitecture.X86 ? "x86" : "x64";
        public static string GetJavaDir(int majorVersion, JavaArchitecture? architecture = null)
        {
            var arch = ResolveArchitecture(majorVersion, architecture);
            return System.IO.Path.Combine(GetJavaInstallDir(), $"java-{majorVersion}-{ArchSuffix(arch)}");
        }
        private static string GetLegacyJavaDir(int majorVersion)
            => System.IO.Path.Combine(GetJavaInstallDir(), $"java-{majorVersion}");
        public static bool IsJavaInstalled(int majorVersion, JavaArchitecture? architecture = null)
            => GetJavaPath(majorVersion, architecture) != null;
        public static string? GetJavaPath(int majorVersion, JavaArchitecture? architecture = null)
        {
            foreach (var dir in new[] { GetJavaDir(majorVersion, architecture), GetLegacyJavaDir(majorVersion) })
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    var javaw = Directory.GetFiles(dir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (javaw != null) return javaw;
                }
                catch { }
            }
            return null;
        }
        public static async Task InstallJavaAsync(int majorVersion, JavaArchitecture? architecture = null)
        {
            var arch = ResolveArchitecture(majorVersion, architecture);
            if (!AdoptiumUrls.TryGetValue((majorVersion, arch), out var url))
                throw new Exception($"No download URL configured for Java {majorVersion} ({arch})");
            var installDir = GetJavaDir(majorVersion, arch);
            if (Directory.Exists(installDir))
            {
                try { Directory.Delete(installDir, true); } catch { }
            }
            Directory.CreateDirectory(installDir);
            ReportProgress($"Downloading Java {majorVersion} ({ArchSuffix(arch)})...");
            ReportPercent(10);
            var tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oranglauncher_java{majorVersion}_{ArchSuffix(arch)}.zip");
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                long downloaded = 0;
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloaded += bytesRead;
                    if (totalBytes > 0)
                    {
                        var pct = (int)(10 + downloaded * 60.0 / totalBytes);
                        ReportPercent(pct);
                    }
                }
                ReportProgress($"Extracting Java {majorVersion} ({ArchSuffix(arch)})...");
                ReportPercent(75);
                fileStream.Close();
                ZipFile.ExtractToDirectory(tempZip, installDir, true);
                ReportProgress($"Java {majorVersion} ({ArchSuffix(arch)}) installed successfully!");
                ReportPercent(100);
                Debug.WriteLine($"[JavaManager] Java {majorVersion} ({arch}) installed to {installDir}");
            }
            finally
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
        public static void UninstallJava(int majorVersion, JavaArchitecture? architecture = null)
        {
            var dir = GetJavaDir(majorVersion, architecture);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
                Debug.WriteLine($"[JavaManager] Uninstalled Java {majorVersion} from {dir}");
            }
        }
        public static List<JavaInstallation> GetAllJavaInstallations()
        {
            var results = new List<JavaInstallation>();
            foreach (var version in new[] { 8, 17, 21, 25 })
            {
                foreach (var arch in GetAvailableArchitectures(version))
                {
                    var dir = GetJavaDir(version, arch);
                    if (!Directory.Exists(dir)) continue;
                    var path = Directory.Exists(dir) ? Directory.GetFiles(dir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault() : null;
                    if (path != null)
                    {
                        results.Add(new JavaInstallation
                        {
                            MajorVersion = version,
                            Path = path,
                            DisplayName = $"Java {version} ({ArchSuffix(arch)} - Adoptium - Managed)",
                            IsManaged = true
                        });
                    }
                }
                var legacyDir = GetLegacyJavaDir(version);
                if (Directory.Exists(legacyDir))
                {
                    try
                    {
                        var path = Directory.GetFiles(legacyDir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (path != null && !results.Any(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                        {
                            results.Add(new JavaInstallation
                            {
                                MajorVersion = version,
                                Path = path,
                                DisplayName = $"Java {version} (Adoptium - Managed)",
                                IsManaged = true
                            });
                        }
                    }
                    catch { }
                }
            }
            var mcRuntime = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".minecraft", "runtime");
            if (Directory.Exists(mcRuntime))
            {
                var runtimeMap = new Dictionary<string, int>
                {
                    ["java-runtime-alpha"] = 8,
                    ["java-runtime-beta"] = 16,
                    ["java-runtime-gamma"] = 17,
                    ["java-runtime-delta"] = 21,
                };
                foreach (var (component, ver) in runtimeMap)
                {
                    try
                    {
                        var compDir = System.IO.Path.Combine(mcRuntime, component);
                        if (Directory.Exists(compDir))
                        {
                            var javaw = Directory.GetFiles(compDir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                            if (javaw != null && !results.Any(r => r.Path.Equals(javaw, StringComparison.OrdinalIgnoreCase)))
                            {
                                results.Add(new JavaInstallation
                                {
                                    MajorVersion = ver,
                                    Path = javaw,
                                    DisplayName = $"Java {ver} (Minecraft Runtime)",
                                    IsManaged = false
                                });
                            }
                        }
                    }
                    catch { }
                }
            }
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                var javaw = System.IO.Path.Combine(javaHome, "bin", "javaw.exe");
                if (File.Exists(javaw) && !results.Any(r => r.Path.Equals(javaw, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new JavaInstallation
                    {
                        MajorVersion = 0,
                        Path = javaw,
                        DisplayName = $"System Java (JAVA_HOME)",
                        IsManaged = false
                    });
                }
            }
            return results;
        }
        public static int[] GetAvailableVersions() => [8, 17, 21, 25];
    }
}