namespace OrangLauncher.Managers
{
    public static class PlatformPaths
    {
        private static readonly object MigrationLock = new();
        private static bool _migrated;

        private static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string GetRootDir()
        {
            var path = Path.Combine(AppData, "OrangStudio", "OrangLauncher");
            EnsureMigrated(path);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetMinecraftDir()
        {
            var path = Path.Combine(GetRootDir(), ".minecraft");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetDataDir()
        {
            var path = Path.Combine(GetRootDir(), "launcher");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetInstancesDir()
        {
            var path = Path.Combine(GetDataDir(), "instances");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void EnsureMigrated(string rootDir)
        {
            if (_migrated) return;
            lock (MigrationLock)
            {
                if (_migrated) return;
                _migrated = true;
                try
                {
                    var newLauncherDir = Path.Combine(rootDir, "launcher");
                    var oldLauncherDir = Path.Combine(AppData, "OrangLauncher");
                    if (!Directory.Exists(newLauncherDir) && Directory.Exists(oldLauncherDir))
                    {
                        Directory.CreateDirectory(rootDir);
                        try { Directory.Move(oldLauncherDir, newLauncherDir); }
                        catch { CopyDirectory(oldLauncherDir, newLauncherDir); }
                    }

                    var newMcDir = Path.Combine(rootDir, ".minecraft");
                    var oldMcDir = Path.Combine(AppData, ".minecraft");
                    if (!Directory.Exists(newMcDir) && Directory.Exists(oldMcDir))
                    {
                        Directory.CreateDirectory(rootDir);
                        try { Directory.Move(oldMcDir, newMcDir); }
                        catch
                        {
                            // Locked, keep using it in place copy
                            CopyDirectory(oldMcDir, newMcDir);
                        }
                    }
                }
                catch { }
            }
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dir.Replace(source, dest));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                try { File.Copy(file, file.Replace(source, dest), false); } catch { }
            }
        }
    }
}
