using System.Reflection;
using OrangLauncher.Models;
namespace OrangLauncher.Managers
{
    public interface ILauncherPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }
        void OnLoad();
        void OnUnload();
        Task OnBeforeGameLaunch(string profileName, string version);
        Task OnAfterGameExit(string profileName, int exitCode);
    }
    public class PluginManager
    {
        private static PluginManager? _instance;
        public static PluginManager Instance => _instance ??= new PluginManager();
        private readonly List<LoadedPlugin> _loadedPlugins = new();
        private class LoadedPlugin
        {
            public string Path { get; set; } = "";
            public Assembly? Assembly { get; set; }
            public ILauncherPlugin? PluginInstance { get; set; }
            public bool IsEnabled { get; set; }
        }
        private Action<string>? _logAction;
        public void SetLogAction(Action<string> logAction)
        {
            _logAction = logAction;
        }
        private void Log(string message)
        {
            _logAction?.Invoke($"[PluginManager] {message}");
        }
        public IReadOnlyList<ILauncherPlugin> LoadedPlugins
        {
            get
            {
                var plugins = new List<ILauncherPlugin>();
                foreach (var loaded in _loadedPlugins)
                {
                    if (loaded.PluginInstance != null && loaded.IsEnabled)
                        plugins.Add(loaded.PluginInstance);
                }
                return plugins;
            }
        }
        public void LoadPlugins(IEnumerable<PluginInfo> plugins)
        {
            UnloadAllPlugins();
            foreach (var pluginInfo in plugins)
            {
                if (!pluginInfo.IsEnabled)
                    continue;
                if (!File.Exists(pluginInfo.Path))
                {
                    Log($"Plugin file not found: {pluginInfo.Path}");
                    continue;
                }
                var extension = Path.GetExtension(pluginInfo.Path).ToLowerInvariant();
                if (extension == ".dll")
                {
                    LoadDllPlugin(pluginInfo.Path);
                }
                else
                {
                    Log($"Unsupported plugin file type: {Path.GetFileName(pluginInfo.Path)} (only .dll is supported)");
                }
            }
        }
        private void LoadDllPlugin(string dllPath)
        {
            try
            {
                Log($"Loading DLL plugin: {Path.GetFileName(dllPath)}");
                byte[] dllBytes = File.ReadAllBytes(dllPath);
                var assembly = Assembly.Load(dllBytes);
                LoadPluginTypesFromAssembly(assembly, dllPath);
            }
            catch (Exception ex)
            {
                Log($"Failed to load DLL plugin {Path.GetFileName(dllPath)}: {ex.Message}");
            }
        }
        private void LoadPluginTypesFromAssembly(Assembly assembly, string sourcePath)
        {
            Type[] candidateTypes;
            try { candidateTypes = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                Log($"Type load issues in {Path.GetFileName(sourcePath)}: {ex.Message}");
                candidateTypes = ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }
            var pluginTypes = new List<Type>();
            foreach (var type in candidateTypes)
            {
                if (type == null) continue;
                if (typeof(ILauncherPlugin).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    pluginTypes.Add(type);
                }
            }
            if (pluginTypes.Count == 0)
            {
                Log($"No ILauncherPlugin implementations found in {Path.GetFileName(sourcePath)}");
                return;
            }
            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var instance = Activator.CreateInstance(pluginType) as ILauncherPlugin;
                    if (instance != null)
                    {
                        instance.OnLoad();
                        _loadedPlugins.Add(new LoadedPlugin
                        {
                            Path = sourcePath,
                            Assembly = assembly,
                            PluginInstance = instance,
                            IsEnabled = true
                        });
                        Log($"Loaded plugin: {instance.Name} v{instance.Version}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Failed to create plugin instance for {pluginType.Name}: {ex.Message}");
                }
            }
        }
        public void UnloadAllPlugins()
        {
            foreach (var loaded in _loadedPlugins)
            {
                try
                {
                    loaded.PluginInstance?.OnUnload();
                    Log($"Unloaded plugin: {loaded.PluginInstance?.Name ?? Path.GetFileName(loaded.Path)}");
                }
                catch (Exception ex)
                {
                    Log($"Error unloading plugin: {ex.Message}");
                }
            }
            _loadedPlugins.Clear();
        }
        public async Task NotifyBeforeGameLaunch(string profileName, string version)
        {
            foreach (var loaded in _loadedPlugins)
            {
                if (loaded.IsEnabled && loaded.PluginInstance != null)
                {
                    try
                    {
                        await loaded.PluginInstance.OnBeforeGameLaunch(profileName, version);
                    }
                    catch (Exception ex)
                    {
                        Log($"Plugin {loaded.PluginInstance.Name} error on BeforeGameLaunch: {ex.Message}");
                    }
                }
            }
        }
        public async Task NotifyAfterGameExit(string profileName, int exitCode)
        {
            foreach (var loaded in _loadedPlugins)
            {
                if (loaded.IsEnabled && loaded.PluginInstance != null)
                {
                    try
                    {
                        await loaded.PluginInstance.OnAfterGameExit(profileName, exitCode);
                    }
                    catch (Exception ex)
                    {
                        Log($"Plugin {loaded.PluginInstance.Name} error on AfterGameExit: {ex.Message}");
                    }
                }
            }
        }
        public List<(string Name, string Version, string Description)> GetLoadedPluginInfo()
        {
            var result = new List<(string, string, string)>();
            foreach (var loaded in _loadedPlugins)
            {
                if (loaded.PluginInstance != null)
                {
                    result.Add((loaded.PluginInstance.Name, loaded.PluginInstance.Version, loaded.PluginInstance.Description));
                }
            }
            return result;
        }
    }
}