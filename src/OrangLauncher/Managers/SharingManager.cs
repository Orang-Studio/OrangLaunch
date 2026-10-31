using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public static class SharingManager
    {
        private sealed record Target(string Key, string SharedName, string RelPath, bool IsFile);
        private static readonly Target[] Targets =
        [
            new("shareOptions", "options.txt", "options.txt", true),
            new("shareResourcepacks", "resourcepacks", "resourcepacks", false),
            new("shareShaderpacks", "shaderpacks", "shaderpacks", false),
            new("shareServers", "servers.dat", "servers.dat", true),
            new("shareScreenshots", "screenshots", "screenshots", false),
        ];
        private static readonly object Lock = new();
        public static event Action<string>? Log;
        public static string SharedDir
        {
            get
            {
                var dir = Path.Combine(PlatformPaths.GetDataDir(), "shared");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
        public static bool AnyEnabled => Targets.Any(t => LauncherSettings.GetBool(t.Key));
        public static void ApplyAll()
        {
            lock (Lock)
            {
                foreach (var inst in InstanceManager.Instance.GetInstances())
                    ApplyUnlocked(inst);
            }
        }
        public static void Apply(MinecraftInstance instance)
        {
            lock (Lock) ApplyUnlocked(instance);
        }
        private static void ApplyUnlocked(MinecraftInstance instance)
        {
            Directory.CreateDirectory(instance.MinecraftDir);
            foreach (var t in Targets)
            {
                var shared = Path.Combine(SharedDir, t.SharedName);
                var local = Path.Combine(instance.MinecraftDir, t.RelPath);
                var enabled = LauncherSettings.GetBool(t.Key);
                try
                {
                    if (t.IsFile) ApplyFile(t, shared, local, enabled);
                    else ApplyDirectory(shared, local, enabled);
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"[Sharing] {t.SharedName} / {instance.Name}: {ex.Message}");
                }
            }
        }
        private static void ApplyDirectory(string shared, string local, bool enabled)
        {
            if (enabled)
            {
                if (FileLinks.IsLink(local))
                {
                    if (FileLinks.PointsTo(local, shared)) return;
                    FileLinks.RemoveLink(local);
                }
                if (!Directory.Exists(shared))
                {
                    if (Directory.Exists(local)) InstanceManager.CopyDirectory(local, shared);
                    else Directory.CreateDirectory(shared);
                }
                if (Directory.Exists(local))
                {
                    MergeInto(local, shared);
                    Directory.Delete(local, true);
                }
                FileLinks.CreateDirectoryJunction(local, shared);
            }
            else if (FileLinks.IsLink(local))
            {
                FileLinks.RemoveLink(local);
                if (Directory.Exists(shared)) InstanceManager.CopyDirectory(shared, local);
                else Directory.CreateDirectory(local);
            }
        }
        private static void MergeInto(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from))
            {
                var dest = Path.Combine(to, Path.GetFileName(file));
                if (!File.Exists(dest)) File.Copy(file, dest);
            }
            foreach (var dir in Directory.GetDirectories(from))
            {
                if (FileLinks.IsLink(dir)) continue;
                var dest = Path.Combine(to, Path.GetFileName(dir));
                if (!Directory.Exists(dest)) InstanceManager.CopyDirectory(dir, dest);
            }
        }
        private static void ApplyFile(Target t, string shared, string local, bool enabled)
        {
            if (!enabled)
            {
                if (FileLinks.IsLink(local))
                {
                    FileLinks.RemoveLink(local);
                    if (File.Exists(shared)) File.Copy(shared, local, true);
                }
                return;
            }
            if (FileLinks.IsLink(local))
            {
                if (File.Exists(FileLinks.Resolve(local)) && !File.Exists(shared))
                    File.Copy(FileLinks.Resolve(local), shared, true);
                FileLinks.RemoveLink(local);
            }
            if (!File.Exists(shared))
            {
                if (File.Exists(local)) File.Copy(local, shared, true);
                else if (t.SharedName == "servers.dat") ServerListManager.Instance.SaveServersToFile(shared, new List<ServerInfo>());
                else File.WriteAllText(shared, "");
            }
            if (!File.Exists(local) || File.GetLastWriteTimeUtc(shared) > File.GetLastWriteTimeUtc(local))
                File.Copy(shared, local, true);
        }
        public static void PullBeforeLaunch(MinecraftInstance instance) => Apply(instance);
        public static void PushAfterExit(MinecraftInstance instance)
        {
            lock (Lock)
            {
                foreach (var t in Targets.Where(t => t.IsFile && LauncherSettings.GetBool(t.Key)))
                {
                    var shared = Path.Combine(SharedDir, t.SharedName);
                    var local = Path.Combine(instance.MinecraftDir, t.RelPath);
                    try
                    {
                        if (File.Exists(local) && !FileLinks.IsLink(local) &&
                            (!File.Exists(shared) || File.GetLastWriteTimeUtc(local) > File.GetLastWriteTimeUtc(shared)))
                            File.Copy(local, shared, true);
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"[Sharing] {t.SharedName} / {instance.Name}: {ex.Message}");
                    }
                }
                ApplyUnlocked(instance);
            }
        }
    }
}
