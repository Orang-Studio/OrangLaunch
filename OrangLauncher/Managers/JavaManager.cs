using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
namespace OrangLauncher.Managers
{
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
        private static readonly Dictionary<int, string> AdoptiumUrls = new()
        {
            [8] = "https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u472-b08/OpenJDK8U-jre_x86-32_windows_hotspot_8u472b08.zip",
            [17] = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.17%2B10/OpenJDK17U-jre_x86-32_windows_hotspot_17.0.17_10.zip",
            [21] = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.10%2B7/OpenJDK21U-jre_x64_windows_hotspot_21.0.10_7.zip",
            [25] = "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.2%2B10/OpenJDK25U-jre_x64_windows_hotspot_25.0.2_10.zip"
        };
        public static string GetJavaInstallDir()
        {
            var dir = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "java");
            Directory.CreateDirectory(dir);
            return dir;
        }
        public static string GetJavaDir(int majorVersion)
        {
            return System.IO.Path.Combine(GetJavaInstallDir(), $"java-{majorVersion}");
        }
        public static bool IsJavaInstalled(int majorVersion)
        {
            var dir = GetJavaDir(majorVersion);
            if (!Directory.Exists(dir)) return false;
            try
            {
                var javaw = Directory.GetFiles(dir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                return javaw != null;
            }
            catch { return false; }
        }
        public static string? GetJavaPath(int majorVersion)
        {
            var dir = GetJavaDir(majorVersion);
            if (!Directory.Exists(dir)) return null;
            try
            {
                return Directory.GetFiles(dir, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
            }
            catch { return null; }
        }
        public static async Task InstallJavaAsync(int majorVersion)
        {
            if (!AdoptiumUrls.TryGetValue(majorVersion, out var url))
                throw new Exception($"No download URL configured for Java {majorVersion}");
            var installDir = GetJavaDir(majorVersion);
            if (Directory.Exists(installDir))
            {
                try { Directory.Delete(installDir, true); } catch { }
            }
            Directory.CreateDirectory(installDir);
            ReportProgress($"Downloading Java {majorVersion}...");
            ReportPercent(10);
            var tempZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oranglauncher_java{majorVersion}.zip");
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
                ReportProgress($"Extracting Java {majorVersion}...");
                ReportPercent(75);
                fileStream.Close();
                ZipFile.ExtractToDirectory(tempZip, installDir, true);
                ReportProgress($"Java {majorVersion} installed successfully!");
                ReportPercent(100);
                Debug.WriteLine($"[JavaManager] Java {majorVersion} installed to {installDir}");
            }
            finally
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
        public static void UninstallJava(int majorVersion)
        {
            var dir = GetJavaDir(majorVersion);
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
                var path = GetJavaPath(version);
                if (path != null)
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