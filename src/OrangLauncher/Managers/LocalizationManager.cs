namespace OrangLauncher.Managers
{
    public static class LocalizationManager
    {
        private static Dictionary<string, string> _strings = new();
        private static Dictionary<string, string> _fallback = new();
        private static string _currentLanguage = "en-US";
        public static string CurrentLanguage => _currentLanguage;
        private static IEnumerable<string> LocaleDirs()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            yield return Path.Combine(baseDir, "Other", "locales");
            yield return Path.Combine(baseDir, "locales");
            yield return Path.Combine(baseDir, "..", "Other", "locales");
            yield return Path.Combine(baseDir, "..", "..", "Other", "locales");
            yield return Path.Combine(baseDir, "..", "..", "..", "Other", "locales");
        }
        private static Dictionary<string, string>? ReadLocale(string languageCode)
        {
            foreach (var dir in LocaleDirs())
            {
                var fullPath = Path.GetFullPath(Path.Combine(dir, $"{languageCode}.locale"));
                if (!File.Exists(fullPath)) continue;
                try
                {
                    var result = new Dictionary<string, string>();
                    foreach (var line in File.ReadAllLines(fullPath, System.Text.Encoding.UTF8))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                            result[parts[0].Trim()] = parts[1].Trim().Replace("\\n", "\n");
                    }
                    return result;
                }
                catch { }
            }
            return null;
        }
        public static void LoadLanguage(string languageCode)
        {
            _currentLanguage = languageCode;
            _fallback = ReadLocale("en-US") ?? new Dictionary<string, string>();
            _strings = languageCode == "en-US" ? _fallback : ReadLocale(languageCode) ?? _fallback;
        }
        public static string GetString(string key, string defaultValue = "")
        {
            if (_strings.TryGetValue(key, out var value) && value.Length > 0) return value;
            if (_fallback.TryGetValue(key, out var fb) && fb.Length > 0) return fb;
            return defaultValue;
        }
        public static string T(string key, string defaultValue, params (string Name, object? Value)[] args)
        {
            var text = GetString(key, defaultValue);
            foreach (var (name, value) in args)
                text = text.Replace("{" + name + "}", value?.ToString() ?? "");
            return text;
        }
        public static List<string> GetAvailableLanguages()
        {
            var languages = new List<string> { "en-US" };
            foreach (var dir in LocaleDirs())
            {
                try
                {
                    var fullDir = Path.GetFullPath(dir);
                    if (!Directory.Exists(fullDir)) continue;
                    foreach (var file in Directory.GetFiles(fullDir, "*.locale"))
                    {
                        var langCode = Path.GetFileNameWithoutExtension(file);
                        if (!languages.Contains(langCode))
                            languages.Add(langCode);
                    }
                }
                catch { }
            }
            return languages;
        }
        public static string GetLanguageDisplayName(string code)
        {
            return code switch
            {
                "en-US" => "English (United States)",
                "lt-LT" => "Lietuvių (Lithuania)",
                "ru-RU" => "Русский (Russia)",
                "pl-PL" => "Polski (Poland)",
                "de-DE" => "Deutsch (Germany)",
                "lv-LV" => "Latviešu (Latvia)",
                _ => code
            };
}   }   }