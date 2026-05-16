using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public class InstanceManager
    {
        private static InstanceManager? _instance;
        public static InstanceManager Instance => _instance ??= new InstanceManager();
        private List<MinecraftInstance> _instances = new();
        private string? _selectedId;
        private readonly string _instancesPath;
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
        }
        public void SaveInstances()
        {
            try
            {
                var data = new InstancesData { Instances = _instances, SelectedId = _selectedId };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_instancesPath, json);
            }
            catch { }
        }
        public List<MinecraftInstance> GetInstances() => _instances.ToList();
        public MinecraftInstance? GetInstance(string id) => _instances.FirstOrDefault(i => i.InstanceId == id);
        public MinecraftInstance? GetInstanceByName(string name) => _instances.FirstOrDefault(i => i.Name == name);
        public MinecraftInstance? GetSelectedInstance()
        {
            return _instances.FirstOrDefault(i => i.InstanceId == _selectedId) ?? _instances.FirstOrDefault();
        }
        public void SetSelectedInstance(string id)
        {
            _selectedId = id;
            SaveInstances();
        }
        public MinecraftInstance? CreateInstance(string name, string version, string modLoader, string ram)
        {
            var instance = new MinecraftInstance
            {
                Name = name,
                Version = version,
                ModLoader = modLoader,
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
                    Icon = "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",
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
        public bool RemoveInstance(string id)
        {
            var instance = _instances.FirstOrDefault(i => i.InstanceId == id);
            if (instance != null)
            {
                _instances.Remove(instance);
                if (_selectedId == id) _selectedId = _instances.FirstOrDefault()?.InstanceId;
                SaveInstances();
                try
                {
                    if (Directory.Exists(instance.BasePath))
                        Directory.Delete(instance.BasePath, true);
                }
                catch { }
                return true;
            }
            return false;
        }
        private class InstancesData
        {
            public List<MinecraftInstance>? Instances { get; set; }
            public string? SelectedId { get; set; }
        }
    }
}