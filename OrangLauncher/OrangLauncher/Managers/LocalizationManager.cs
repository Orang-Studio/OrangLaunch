namespace OrangLauncher.Managers
{
    public static class LocalizationManager
    {
        private static Dictionary<string, string> _strings = new();
        private static string _currentLanguage = "en-US";
        public static void LoadLanguage(string languageCode)
        {
            _currentLanguage = languageCode;
            _strings.Clear();
            string[] searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "locales", $"{languageCode}.locale"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales", $"{languageCode}.locale"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Other", "locales", $"{languageCode}.locale"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Other", "locales", $"{languageCode}.locale"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Other", "locales", $"{languageCode}.locale")
            };
            foreach (var path in searchPaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(fullPath, System.Text.Encoding.UTF8);
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                _strings[parts[0].Trim()] = parts[1].Trim();
                            }
                        }
                        return;
                    }
                    catch { }
                }
            }
            LoadDefaults();
        }
        private static void LoadDefaults()
        {
            _strings["UPDATE_NOTES"] = "Update Notes";
            _strings["LAUNCHER_LOG"] = "Launcher Log";
            _strings["GAME_PROFILES"] = "Game Profiles";
            _strings["MODS"] = "Mods";
            _strings["RESOURCE_PACKS"] = "Resource & Shader Packs";
            _strings["SETTINGS"] = "Settings";
            _strings["PROFILE"] = "PROFILE";
            _strings["GAME_PROFILES_TITLE"] = "GAME PROFILES";
            _strings["PLAY"] = "PLAY";
            _strings["NEW_PROFILE"] = "NEW PROFILE";
            _strings["GENERAL"] = "General";
            _strings["ACCOUNTS"] = "Accounts";
            _strings["ADVANCED"] = "Advanced";
            _strings["ABOUT"] = "About";
            _strings["LANGUAGE_CHANGED_MSG"] = "Language changed. Restart the application to apply changes?";
            _strings["LANGUAGE_CHANGED_TITLE"] = "Language Changed";
        }
        public static string GetString(string key, string defaultValue = "")
        {
            return _strings.TryGetValue(key, out var value) ? value : defaultValue;
        }
        public static List<string> GetAvailableLanguages()
        {
            var languages = new List<string> { "en-US" };
            string[] searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "locales"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Other", "locales"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Other", "locales"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Other", "locales")
            };
            foreach (var dir in searchPaths)
            {
                try
                {
                    var fullDir = Path.GetFullPath(dir);
                    if (Directory.Exists(fullDir))
                    {
                        var files = Directory.GetFiles(fullDir, "*.locale");
                        foreach (var file in files)
                        {
                            var langCode = Path.GetFileNameWithoutExtension(file);
                            if (!languages.Contains(langCode))
                                languages.Add(langCode);
                        }
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
        }
    }
}