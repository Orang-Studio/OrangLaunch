using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
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
                .Where(f => f.EndsWith(".jar") || f.EndsWith(".jar.disabled"));
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                mods.Add(new ModInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file).Replace(".jar", ""),
                    Path = file,
                    Size = FormatSize(fi.Length),
                    IsEnabled = !file.EndsWith(".disabled")
                });
            }
            return mods;
        }
        public void AddMods(string modsPath)
        {
            Directory.CreateDirectory(modsPath);
            var dialog = new OpenFileDialog
            {
                Title = "Select Mods",
                Filter = "Mod Files (*.jar)|*.jar|All Files (*.*)|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    var destPath = Path.Combine(modsPath, Path.GetFileName(file));
                    File.Copy(file, destPath, true);
                }
            }
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
        }
    }
}