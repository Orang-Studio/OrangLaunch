using System.IO.Compression;
using System.Text.Json;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public class InstanceManager
    {
        private const string DefaultServerIcon = "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";
        private static InstanceManager? _instance;
        public static InstanceManager Instance => _instance ??= new InstanceManager();
        private List<MinecraftInstance> _instances = new();
        private string? _selectedId;
        private readonly string _instancesPath;
        private readonly object _saveLock = new();
        public event Action? Changed;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private InstanceManager()
        {
            _instancesPath = Path.Combine(PlatformPaths.GetDataDir(), "instances.json");
            LoadInstances();
        }
        private void LoadInstances()
        {
            try
            {
                if (File.Exists(_instancesPath))
                {
                    var json = File.ReadAllText(_instancesPath);
                    var data = JsonSerializer.Deserialize<InstancesData>(json);
                    if (data != null)
                    {
                        _instances = data.Instances ?? new List<MinecraftInstance>();
                        _selectedId = data.SelectedId;
                    }
                }
            }
            catch { }
            foreach (var inst in _instances)
                Migrate(inst);
        }
        private static void Migrate(MinecraftInstance inst)
        {
            inst.Options ??= new InstanceOptions();
            inst.EnvVars ??= "";
            inst.LoaderVersion ??= "";
            if (string.IsNullOrEmpty(inst.Options.JvmArgs) && !string.IsNullOrWhiteSpace(inst.JavaArgs))
            {
                var kept = inst.JavaArgs.Replace("System.Collections.Generic.List`1[System.String]", "")
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(a => !a.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase) && !a.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase));
                inst.Options.JvmArgs = string.Join(" ", kept);
                inst.JavaArgs = "";
            }
            if (string.IsNullOrEmpty(inst.Icon))
            {
                var png = Path.Combine(inst.BasePath, "icon.png");
                if (File.Exists(png)) inst.Icon = png;
            }
        }
        public void SaveInstances()
        {
            lock (_saveLock)
            {
                try
                {
                    var data = new InstancesData { Instances = _instances, SelectedId = _selectedId };
                    var json = JsonSerializer.Serialize(data, JsonOptions);
                    var tmp = _instancesPath + ".tmp";
                    File.WriteAllText(tmp, json);
                    File.Move(tmp, _instancesPath, true);
                }
                catch { }
            }
            Changed?.Invoke();
        }
        public List<MinecraftInstance> GetInstances() => _instances.ToList();
        public MinecraftInstance? GetInstanceByName(string name) => _instances.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        public string? SelectedId => GetSelectedInstance()?.InstanceId;
        public MinecraftInstance? GetSelectedInstance()
        {
            return _instances.FirstOrDefault(i => i.InstanceId == _selectedId) ?? _instances.FirstOrDefault();
        }
        public void SetSelectedInstance(string id)
        {
            if (_selectedId == id) return;
            _selectedId = id;
            SaveInstances();
        }
        public string UniqueName(string baseName)
        {
            var candidate = baseName;
            int i = 2;
            while (GetInstanceByName(candidate) != null)
                candidate = $"{baseName} {i++}";
            return candidate;
        }
        public MinecraftInstance? CreateInstance(string name, string version, string modLoader, string ram, string? loaderVersion = null)
        {
            var instance = new MinecraftInstance
            {
                Name = UniqueName(string.IsNullOrWhiteSpace(name) ? "Instance" : name.Trim()),
                Version = version,
                ModLoader = string.IsNullOrWhiteSpace(modLoader) ? "vanilla" : modLoader.Trim().ToLowerInvariant(),
                LoaderVersion = loaderVersion ?? "",
                Ram = ram
            };
            Directory.CreateDirectory(instance.BasePath);
            Directory.CreateDirectory(instance.MinecraftDir);
            Directory.CreateDirectory(instance.ModsDir);
            Directory.CreateDirectory(instance.ResourcePacksDir);
            Directory.CreateDirectory(instance.ShaderPacksDir);
            try
            {
                var defaultServer = new ServerInfo
                {
                    Name = "Orange MC - Anarchy for all!",
                    Ip = "mc.oranges.lt",
                    Icon = DefaultServerIcon,
                    AcceptTextures = 1
                };
                ServerListManager.Instance.SaveServers(instance.MinecraftDir, [defaultServer]);
            }
            catch { }
            _instances.Add(instance);
            if (_instances.Count == 1) _selectedId = instance.InstanceId;
            SaveInstances();
            return instance;
        }
        public void SetIcon(MinecraftInstance instance, string? sourcePath)
        {
            var dest = Path.Combine(instance.BasePath, "icon.png");
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                instance.Icon = null;
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                return;
            }
            Directory.CreateDirectory(instance.BasePath);
            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                File.Copy(sourcePath, dest, true);
            instance.Icon = dest;
        }
        public MinecraftInstance Duplicate(MinecraftInstance source)
        {
            var dup = new MinecraftInstance
            {
                Name = UniqueName($"{source.Name} Copy"),
                Version = source.Version,
                ModLoader = source.ModLoader,
                LoaderVersion = source.LoaderVersion,
                Ram = source.Ram,
                JavaPath = source.JavaPath,
                EnvVars = source.EnvVars,
                InstalledVersionName = source.InstalledVersionName,
                Options = source.Options.Clone()
            };
            Directory.CreateDirectory(dup.MinecraftDir);
            foreach (var rel in new[] { "mods", "config", "resourcepacks", "shaderpacks", "saves", "options.txt", "servers.dat" })
            {
                var src = Path.Combine(source.MinecraftDir, rel);
                var dst = Path.Combine(dup.MinecraftDir, rel);
                try
                {
                    if (FileLinks.IsLink(src)) continue;
                    if (Directory.Exists(src)) CopyDirectory(src, dst);
                    else if (File.Exists(src)) File.Copy(src, dst, true);
                }
                catch { }
            }
            foreach (var extra in new[] { "icon.png", "modpack.json" })
            {
                var src = Path.Combine(source.BasePath, extra);
                try { if (File.Exists(src)) File.Copy(src, Path.Combine(dup.BasePath, extra), true); } catch { }
            }
            if (File.Exists(Path.Combine(dup.BasePath, "icon.png"))) dup.Icon = Path.Combine(dup.BasePath, "icon.png");
            else dup.Icon = source.Icon;
            _instances.Add(dup);
            _selectedId = dup.InstanceId;
            SaveInstances();
            return dup;
        }
        public MinecraftInstance ImportInstanceZip(string zipPath)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.GetEntry("instance.json") ?? throw new InvalidDataException(LocalizationManager.GetString("QT_NOT_AN_EXPORT", "instance.json not found in zip - not a valid OrangLauncher export."));
            MinecraftInstance? data;
            using (var s = entry.Open())
                data = JsonSerializer.Deserialize<MinecraftInstance>(s);
            if (data == null) throw new InvalidDataException("instance.json");
            data.InstanceId = Guid.NewGuid().ToString();
            data.Name = UniqueName(string.IsNullOrWhiteSpace(data.Name) ? "Imported" : data.Name);
            data.Icon = null;
            Migrate(data);
            var dest = data.BasePath;
            Directory.CreateDirectory(dest);
            var destFull = Path.GetFullPath(dest + Path.DirectorySeparatorChar);
            foreach (var e in zip.Entries)
            {
                if (string.IsNullOrEmpty(e.Name) || e.FullName == "instance.json") continue;
                var target = Path.GetFullPath(Path.Combine(dest, e.FullName));
                if (!target.StartsWith(destFull, StringComparison.OrdinalIgnoreCase)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                e.ExtractToFile(target, true);
            }
            Directory.CreateDirectory(data.MinecraftDir);
            var icon = Path.Combine(dest, "icon.png");
            if (File.Exists(icon)) data.Icon = icon;
            _instances.Add(data);
            _selectedId = data.InstanceId;
            SaveInstances();
            return data;
        }
        public static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                if (FileLinks.IsLink(dir)) continue;
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }
        public bool RemoveInstance(string id)
        {
            var instance = _instances.FirstOrDefault(i => i.InstanceId == id);
            if (instance == null) return false;
            _instances.Remove(instance);
            if (_selectedId == id) _selectedId = _instances.FirstOrDefault()?.InstanceId;
            SaveInstances();
            try
            {
                FileLinks.RemoveLinksUnder(instance.MinecraftDir);
                if (Directory.Exists(instance.BasePath))
                    Directory.Delete(instance.BasePath, true);
            }
            catch { }
            return true;
        }
        private class InstancesData
        {
            public List<MinecraftInstance>? Instances { get; set; }
            public string? SelectedId { get; set; }
        }
    }
}