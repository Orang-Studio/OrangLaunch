using System.Text.RegularExpressions;
namespace OrangLauncher.Managers
{
    public static class McVersion
    {
        public static int[] Parse(string? version)
        {
            if (string.IsNullOrWhiteSpace(version)) return Array.Empty<int>();
            var m = Regex.Match(version, @"^(\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) return Array.Empty<int>();
            var parts = new List<int> { int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value) };
            if (m.Groups[3].Success) parts.Add(int.Parse(m.Groups[3].Value));
            while (parts.Count < 3) parts.Add(0);
            return parts.ToArray();
        }
        public static int Compare(string? a, params int[] b)
        {
            var pa = Parse(a);
            if (pa.Length == 0) return 1;
            for (int i = 0; i < 3; i++)
            {
                var x = i < pa.Length ? pa[i] : 0;
                var y = i < b.Length ? b[i] : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
        public static bool Below(string? version, params int[] b) => Parse(version).Length > 0 && Compare(version, b) < 0;
    }
    public static class JavaResolver
    {
        public static int RequiredJavaFor(string? mcVersion)
        {
            var p = McVersion.Parse(mcVersion);
            if (p.Length == 0) return 21;
            if (p[0] >= 2) return 25;
            if (p[1] < 17) return 8;
            if (p[1] == 17) return 16;
            if (McVersion.Below(mcVersion, 1, 20, 5)) return 17;
            return 21;
        }
        public static int DownloadableMajor(int major) => major switch
        {
            <= 8 => 8,
            <= 17 => 17,
            <= 21 => 21,
            _ => 25
        };
        public static int? ProbeMajor(string javaExe)
        {
            try
            {
                var bin = Path.GetDirectoryName(javaExe);
                var home = bin == null ? null : Path.GetDirectoryName(bin);
                if (home == null) return null;
                var release = Path.Combine(home, "release");
                if (File.Exists(release))
                {
                    foreach (var line in File.ReadAllLines(release))
                    {
                        if (!line.StartsWith("JAVA_VERSION=")) continue;
                        var v = line["JAVA_VERSION=".Length..].Trim('"');
                        var parts = v.Split('.', '_', '+', '-');
                        if (parts.Length > 0 && int.TryParse(parts[0], out var first))
                        {
                            if (first == 1 && parts.Length > 1 && int.TryParse(parts[1], out var second)) return second;
                            return first;
                        }
                    }
                }
                var name = home.ToLowerInvariant();
                var m = Regex.Match(name, @"(?:jdk|jre|java)[-_]?(?:1\.)?(\d+)");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var guess)) return guess;
            }
            catch { }
            return null;
        }
        private static IEnumerable<string> CandidateExecutables()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> FromDir(string dir, int depth)
            {
                if (!Directory.Exists(dir)) yield break;
                string[] files;
                try { files = Directory.GetFiles(dir, "javaw.exe", depth > 0 ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly); }
                catch { yield break; }
                foreach (var f in files) yield return f;
            }
            var roots = new List<string> { JavaManager.GetJavaInstallDir() };
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome)) roots.Add(Path.Combine(javaHome, "bin"));
            roots.Add(Path.Combine(PlatformPaths.GetMinecraftDir(), "runtime"));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "runtime"));
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (var vendor in new[] { "Java", "Eclipse Adoptium", "AdoptOpenJDK", "Zulu", "Microsoft", "BellSoft", "Amazon Corretto" })
            {
                roots.Add(Path.Combine(pf, vendor));
                roots.Add(Path.Combine(pf86, vendor));
            }
            roots.Add(Path.Combine(local, "Programs", "Eclipse Adoptium"));
            foreach (var root in roots)
                foreach (var f in FromDir(root, 1))
                    if (seen.Add(f)) yield return f;
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                string javaw;
                try { javaw = Path.Combine(dir.Trim(), "javaw.exe"); } catch { continue; }
                if (File.Exists(javaw) && seen.Add(javaw)) yield return javaw;
            }
        }
        public static List<(int Major, string Path)> FindAll()
        {
            var result = new List<(int, string)>();
            foreach (var exe in CandidateExecutables())
            {
                var major = ProbeMajor(exe);
                if (major.HasValue) result.Add((major.Value, exe));
            }
            return result;
        }
        public static string? Find(int major, bool exactOnly = false)
        {
            var target = DownloadableMajor(major);
            var managed = JavaManager.GetJavaPath(target);
            if (managed != null && (ProbeMajor(managed) ?? target) == target && (target == major || major == 16)) return managed;
            var all = FindAll();
            var exact = all.FirstOrDefault(j => j.Major == major || (major == 16 && j.Major == 17));
            if (exact.Path != null) return exact.Path;
            if (major >= 16 && !exactOnly)
            {
                var higher = all.Where(j => j.Major > major).OrderBy(j => j.Major).FirstOrDefault();
                if (higher.Path != null) return higher.Path;
            }
            return null;
        }
        public static async Task<string> EnsureAsync(int major, Action<string>? log = null)
        {
            var found = Find(major, exactOnly: true);
            if (found != null) return found;
            var target = DownloadableMajor(major);
            log?.Invoke($"[Java] Java {major} not found, downloading Java {target} from Adoptium...");
            try
            {
                await JavaManager.InstallJavaAsync(target);
                var installed = JavaManager.GetJavaPath(target);
                if (installed != null) return installed;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Java] Download failed: {ex.Message}");
            }
            return Find(major) ?? throw new Exception($"Java {target} could not be installed");
}   }   }