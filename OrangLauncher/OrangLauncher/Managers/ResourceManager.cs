using OrangLauncher.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;
namespace OrangLauncher.Managers
{
    public class ResourceManager
    {
        private static ResourceManager? _instance;
        public static ResourceManager Instance => _instance ??= new ResourceManager();
        public List<ResourcePackInfo> GetPacks(string path, bool isShader)
        {
            var packs = new List<ResourcePackInfo>();
            if (!Directory.Exists(path)) return packs;
            var files = Directory.GetFiles(path, "*.*")
                .Where(f => f.EndsWith(".zip") || f.EndsWith(".jar") || Directory.Exists(f));
            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                packs.Add(new ResourcePackInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Path = file,
                    Size = FormatSize(fi.Length)
                });
            }
            foreach (var dir in Directory.GetDirectories(path))
            {
                packs.Add(new ResourcePackInfo
                {
                    Name = Path.GetFileName(dir),
                    Path = dir,
                    Size = "Folder"
                });
            }
            return packs;
        }
        public async void AddPacks(string path, bool isShader)
        {
            Directory.CreateDirectory(path);
            var picker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(App.MainAppWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".zip");
            picker.FileTypeFilter.Add("*");
            var files = await picker.PickMultipleFilesAsync();
            if (files != null)
            {
                foreach (var file in files)
                {
                    var destPath = Path.Combine(path, file.Name);
                    File.Copy(file.Path, destPath, true);
                }
            }
        }
        public void RemovePacks(List<ResourcePackInfo> packs)
        {
            foreach (var pack in packs)
            {
                if (File.Exists(pack.Path))
                    File.Delete(pack.Path);
                else if (Directory.Exists(pack.Path))
                    Directory.Delete(pack.Path, true);
            }
        }
        public void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start("explorer.exe", path);
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