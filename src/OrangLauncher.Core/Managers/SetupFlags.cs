using System.Text.Json;

namespace OrangLauncher.Managers
{
    public static class SetupFlags
    {
        private static string ConfigPath => Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json");

        public static bool IsSetupDone()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return false;
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(ConfigPath));
                return config != null && config.TryGetValue("setupDone", out var v) && v.ValueKind == JsonValueKind.True;
            }
            catch { return false; }
        }

        public static void MarkSetupDone()
        {
            try
            {
                Dictionary<string, object> config = new();
                if (File.Exists(ConfigPath))
                {
                    var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(ConfigPath));
                    if (existing != null)
                        foreach (var kv in existing) config[kv.Key] = kv.Value;
                }
                config["setupDone"] = true;
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
}   }   }