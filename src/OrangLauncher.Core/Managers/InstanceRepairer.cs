using OrangLauncher.Models;

namespace OrangLauncher.Managers
{
    public static class InstanceRepairer
    {
        private static readonly string[] LoaderMarkers = { "fabric", "forge", "neoforge", "quilt", "optifine", "liteloader" };

        private static bool LooksModded(string version) =>
            LoaderMarkers.Any(m => version.Contains(m, StringComparison.OrdinalIgnoreCase));

        private static bool HasRealLoader(string loader) =>
            !string.IsNullOrEmpty(loader) &&
            !loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase) &&
            !loader.Equals("none", StringComparison.OrdinalIgnoreCase);
        private static string ExtractBaseVersion(string version)
        {
            if (!LooksModded(version)) return version;
            var parts = version.Split('-');
            foreach (var part in parts.Reverse())
                if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^\d+\.\d+(\.\d+)?$"))
                    return part;
            foreach (var part in parts)
                if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^\d+\.\d+(\.\d+)?$"))
                    return part;
            return version;
        }

        private static bool VersionJsonExists(string versionsDir, string versionName)
        {
            var dir = Path.Combine(versionsDir, versionName);
            return Directory.Exists(dir) && Directory.GetFiles(dir, "*.json").Length > 0;
        }
        private static string? FindInstalledLoaderVersion(string versionsDir, string mcVersion, string loader)
        {
            if (!Directory.Exists(versionsDir)) return null;
            foreach (var dir in Directory.GetDirectories(versionsDir))
            {
                var name = Path.GetFileName(dir);
                if (name == null) continue;
                if (!name.Contains(loader, StringComparison.OrdinalIgnoreCase)) continue;
                if (!name.Contains(mcVersion, StringComparison.Ordinal)) continue;
                if (File.Exists(Path.Combine(dir, $"{name}.json")))
                    return name;
            }
            return null;
        }

        public static bool IsBroken(MinecraftInstance instance, string versionsDir)
        {
            if (!HasRealLoader(instance.ModLoader)) return false;
            var effective = !string.IsNullOrEmpty(instance.InstalledVersionName)
                ? instance.InstalledVersionName!
                : instance.Version;
            // bare game version writed for a modded profile
            // id whouse files are gone.
            if (!LooksModded(effective)) return true;
            return !VersionJsonExists(versionsDir, effective);
        }

        public static async Task<int> RepairAllAsync(Action<string>? log = null, bool installMissing = true)
        {
            int repaired = 0;
            try
            {
                var versionsDir = Path.Combine(PlatformPaths.GetMinecraftDir(), "versions");
                foreach (var instance in InstanceManager.Instance.GetInstances())
                {
                    try
                    {
                        if (!IsBroken(instance, versionsDir)) continue;
                        var loader = instance.ModLoader.ToLowerInvariant();
                        var mcVersion = ExtractBaseVersion(
                            !string.IsNullOrEmpty(instance.InstalledVersionName) ? instance.InstalledVersionName! : instance.Version);
                        log?.Invoke($"[Repair] '{instance.Name}': {loader} not installed for {mcVersion}");

                        var installed = FindInstalledLoaderVersion(versionsDir, mcVersion, loader);
                        if (installed == null && installMissing)
                        {
                            log?.Invoke($"[Repair] Installing {loader} for {mcVersion}...");
                            installed = await ModLoaderInstaller.InstallLoaderAsync(
                                PlatformPaths.GetMinecraftDir(), mcVersion, loader, null);
                        }
                        if (installed == null)
                        {
                            log?.Invoke($"[Repair] '{instance.Name}': no installed {loader} version found, skipped");
                            continue;
                        }
                        var real = InstanceManager.Instance.GetInstance(instance.InstanceId);
                        if (real == null) continue;
                        real.Version = installed;
                        real.InstalledVersionName = installed;
                        repaired++;
                        log?.Invoke($"[Repair] '{instance.Name}' fixed → {installed}");
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"[Repair] '{instance.Name}' failed: {ex.Message}");
                    }
                }
                if (repaired > 0) InstanceManager.Instance.SaveInstances();
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Repair] Scan failed: {ex.Message}");
            }
            return repaired;
        }
    }
}