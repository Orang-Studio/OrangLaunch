using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public class ModManager
    {
        private static ModManager? _instance;
        public static ModManager Instance => _instance ??= new ModManager();
        public List<ModInfo> GetMods(string modsPath)
        {
            var mods = new List<ModInfo>();
            if (!Directory.Exists(modsPath)) return mods;
            var files = Directory.GetFiles(modsPath, "*.*")
                .Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".hmod", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".hmod.disabled", StringComparison.OrdinalIgnoreCase));
            var isHorizonMods = string.Equals(Path.GetFileName(modsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "horizon-mods", StringComparison.OrdinalIgnoreCase);
            var instanceDir = Directory.GetParent(modsPath)?.FullName ?? modsPath;
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                var enabled = !file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                string displayName = Path.GetFileNameWithoutExtension(file)
                    .Replace(".jar", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(".hmod", "", StringComparison.OrdinalIgnoreCase);
                if (file.EndsWith(".hmod", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".hmod.disabled", StringComparison.OrdinalIgnoreCase))
                {
                    var hm = HorizonClient.ReadHmodMetadata(file);
                    if (hm != null && !string.IsNullOrWhiteSpace(hm.Name))
                        displayName = hm.Name;
                    if (enabled && isHorizonMods)
                    {
                        var modId = hm?.Id ?? Path.GetFileNameWithoutExtension(file);
                        enabled = HorizonClient.IsModEnabled(instanceDir, modId, true);
                    }
                }
                mods.Add(new ModInfo
                {
                    Name = displayName,
                    Path = file,
                    Size = FormatSize(fi.Length),
                    IsEnabled = enabled
                });
            }
            if (isHorizonMods)
            {
                foreach (var dir in Directory.GetDirectories(modsPath))
                {
                    var hm = HorizonClient.ReadHmodMetadata(dir);
                    if (hm == null) continue;
                    var enabled = HorizonClient.IsModEnabled(instanceDir, hm.Id, true);
                    mods.Add(new ModInfo
                    {
                        Name = string.IsNullOrWhiteSpace(hm.Name) ? Path.GetFileName(dir) : hm.Name, Path = dir, Size = "Folder", IsEnabled = enabled
                    });
                }
            }
            return mods;
        }
        public int AddMods(string modsPath, IEnumerable<string> files)
        {
            Directory.CreateDirectory(modsPath);
            int added = 0;
            foreach (var file in files)
            {
                var destPath = Path.Combine(modsPath, Path.GetFileName(file));
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(file, destPath, true);
                added++;
            }
            return added;
        }
        public void RemoveMods(List<ModInfo> mods)
        {
            foreach (var mod in mods)
            {
                if (File.Exists(mod.Path))
                    File.Delete(mod.Path);
            }
        }
        public void OpenModsFolder(string modsPath)
        {
            Directory.CreateDirectory(modsPath);
            System.Diagnostics.Process.Start("explorer.exe", modsPath);
        }
        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
}   }   }