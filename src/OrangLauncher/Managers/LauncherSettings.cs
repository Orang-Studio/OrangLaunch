using System.Text.Json;
using System.Text.Json.Nodes;
namespace OrangLauncher.Managers
{
    public static class LauncherSettings
    {
        private static readonly object Lock = new();
        private static JsonObject? _data;
        private static string ConfigPath => Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json");
        public static event Action<string>? Changed;
        private static JsonObject Data
        {
            get
            {
                if (_data != null) return _data;
                lock (Lock)
                {
                    if (_data != null) return _data;
                    try
                    {
                        if (File.Exists(ConfigPath))
                            _data = JsonNode.Parse(File.ReadAllText(ConfigPath)) as JsonObject;
                    }
                    catch { }
                    _data ??= new JsonObject();
                    return _data;
                }
            }
        }
        private static void Save()
        {
            lock (Lock)
            {
                try
                {
                    var tmp = ConfigPath + ".tmp";
                    File.WriteAllText(tmp, Data.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    File.Move(tmp, ConfigPath, true);
                }
                catch { }
            }
        }
        public static void Reload()
        {
            lock (Lock) _data = null;
        }
        public static string GetString(string key, string def = "")
        {
            try
            {
                var node = Data[key];
                if (node is JsonValue v && v.TryGetValue<string>(out var s)) return s;
            }
            catch { }
            return def;
        }
        public static bool GetBool(string key, bool def = false)
        {
            try
            {
                var node = Data[key];
                if (node is JsonValue v)
                {
                    if (v.TryGetValue<bool>(out var b)) return b;
                    if (v.TryGetValue<string>(out var s) && bool.TryParse(s, out var p)) return p;
                }
            }
            catch { }
            return def;
        }
        public static void Set(string key, string value)
        {
            lock (Lock) Data[key] = value;
            Save();
            Changed?.Invoke(key);
        }
        public static void Set(string key, bool value)
        {
            lock (Lock) Data[key] = value;
            Save();
            Changed?.Invoke(key);
        }
        public static string Language { get => GetString("language", "en-US"); set => Set("language", value); }
        public static string Theme
        {
            get
            {
                var t = GetString("theme", "arc");
                return t switch { "arc" or "dark_prism" or "light" => t, "dark" => "dark_prism", _ => "arc" };
            }
            set => Set("theme", value);
        }
        public static bool DiscordRpcEnabled { get => GetBool("discordRpcEnabled", true); set => Set("discordRpcEnabled", value); }
        public static bool DeleteTelemetryOnStartup { get => GetBool("deleteTelemetryOnStartup", false); set => Set("deleteTelemetryOnStartup", value); }
        public static bool ModernTitleBar { get => GetBool("modernTitleBar", false); set => Set("modernTitleBar", value); }
        public static bool DebugModeEnabled { get => GetBool("debugModeEnabled", false); set => Set("debugModeEnabled", value); }
        public static bool UseDiscreteGpu { get => GetBool("useDiscreteGpu", false); set => Set("useDiscreteGpu", value); }
        public static string ProxyUrl { get => GetString("proxyUrl"); set => Set("proxyUrl", value); }
        public static string CustomBrowser { get => GetString("customBrowser"); set => Set("customBrowser", value); }
        public static string CustomEditor { get => GetString("customEditor"); set => Set("customEditor", value); }
        public static bool ShowSnapshots { get => GetBool("showSnapshots", false); set => Set("showSnapshots", value); }
        public static bool ShowBetas { get => GetBool("showBetas", false); set => Set("showBetas", value); }
        public static bool ShowAlphas { get => GetBool("showAlphas", false); set => Set("showAlphas", value); }
        public static string AccentColor { get => GetString("accentColor", ""); set => Set("accentColor", value); }
        public static HashSet<string> VisibleVersionTypes()
        {
            var types = new HashSet<string> { "release" };
            if (ShowSnapshots) types.Add("snapshot");
            if (ShowBetas) types.Add("old_beta");
            if (ShowAlphas) types.Add("old_alpha");
            return types;
        }
        public static string NewsBrowser { get => GetString("newsBrowser", "webview2"); set => Set("newsBrowser", value); }
    }
}