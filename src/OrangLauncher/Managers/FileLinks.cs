using System.Diagnostics;
namespace OrangLauncher.Managers
{
    public static class FileLinks
    {
        public static bool IsLink(string path)
        {
            try
            {
                if (Directory.Exists(path)) return new DirectoryInfo(path).LinkTarget != null || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
                if (File.Exists(path)) return new FileInfo(path).LinkTarget != null;
            }
            catch { }
            return false;
        }
        public static string? LinkTarget(string path)
        {
            try
            {
                if (Directory.Exists(path)) return new DirectoryInfo(path).LinkTarget;
                if (File.Exists(path)) return new FileInfo(path).LinkTarget;
            }
            catch { }
            return null;
        }
        public static string Resolve(string path)
        {
            var target = LinkTarget(path);
            if (string.IsNullOrEmpty(target)) return path;
            return Path.IsPathRooted(target) ? target : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, target));
        }
        public static bool PointsTo(string link, string target)
        {
            var t = LinkTarget(link);
            if (string.IsNullOrEmpty(t)) return false;
            static string Norm(string p) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p.StartsWith(@"\??\") ? p[4..] : p));
            return string.Equals(Norm(t), Norm(target), StringComparison.OrdinalIgnoreCase);
        }
        public static void RemoveLink(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, false);
            else if (File.Exists(path)) File.Delete(path);
        }
        public static void RemoveLinksUnder(string root)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    if (IsLink(dir)) RemoveLink(dir);
                }
            }
            catch { }
        }
        public static void CreateDirectoryJunction(string link, string target)
        {
            Directory.CreateDirectory(target);
            try
            {
                Directory.CreateSymbolicLink(link, target);
                return;
            }
            catch { }
            var psi = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("mklink");
            psi.ArgumentList.Add("/J");
            psi.ArgumentList.Add(link);
            psi.ArgumentList.Add(target);
            using var p = Process.Start(psi) ?? throw new IOException("mklink failed to start");
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit(15000);
            if (p.ExitCode != 0 || !Directory.Exists(link))
                throw new IOException($"mklink /J failed: {err.Trim()}");
        }
    }
}