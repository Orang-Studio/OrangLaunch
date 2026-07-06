using OrangLauncher.Models;

namespace OrangLauncher.Managers
{
    /// <summary>
    /// Detects and repairs instances whose mod loader was never really installed
    /// ("fake loaders"): the profile says fabric/forge/... but its Version is a bare
    /// game version (e.g. "26.2") with no matching installed loader version.
    /// Runs at startup; relinks to an already-installed loader version when one
    /// exists, otherwise installs the loader.
    /// </summary>
    public static class InstanceRepairer
    {
        private static readonly string[] LoaderMarkers = { "fabric", "forge", "neoforge", "quilt", "optifine", "liteloader" };

        private static bool LooksModded(string version) =>
            LoaderMarkers.Any(m => version.Contains(m, StringComparison.OrdinalIgnoreCase));

        private static bool HasRealLoader(string loader) =>
            !string.IsNullOrEmpty(loader) &&
            !loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase) &&
            !loader.Equals("none", StringComparison.OrdinalIgnoreCase);

        /// <summary>Base game version of an instance ("26.2" out of "fabric-loader-0.16.9-26.2" or "26.2").</summary>
        private static string ExtractBaseVersion(string version)
        {
            if (!LooksModded(version)) return version;
            // Installed ids are usually "<loader>-...-<mc>" or "<mc>-<loader>-...".
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

        /// <summary>Finds an already-installed modded version folder matching this game version and loader.</summary>
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

        /// <summary>True when the instance claims a loader that is not actually installed/linked.</summary>
        public static bool IsBroken(MinecraftInstance instance, string versionsDir)
        {
            if (!HasRealLoader(instance.ModLoader)) return false;
            var effective = !string.IsNullOrEmpty(instance.InstalledVersionName)
                ? instance.InstalledVersionName!
                : instance.Version;
            // Bare game version recorded for a modded profile, or a modded version
            // id whose files are gone.
            if (!LooksModded(effective)) return true;
            return !VersionJsonExists(versionsDir, effective);
        }

        /// <summary>
        /// Scans all instances and repairs the broken ones. Relinks silently when the
        /// loader is already installed; downloads the loader otherwise. Returns the
        /// number of repaired instances.
        /// </summary>
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
