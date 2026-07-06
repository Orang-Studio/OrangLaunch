using System.Diagnostics;
using System.Text.Json;
namespace OrangLauncher.Managers
{
    /// <summary>
    /// Decides which UI (wpf | winui) the single OrangLauncher.exe boots.
    /// First run auto-detects: Windows 11 (build 22000+) gets WinUI, older gets WPF.
    /// A user choice made in settings is written to ui.json and always wins.
    /// </summary>
    public static class UiModeManager
    {
        public const string Wpf = "wpf";
        public const string WinUi = "winui";
        private static string ConfigPath => Path.Combine(PlatformPaths.GetDataDir(), "ui.json");

        public static string Resolve(string[]? args = null)
        {
            if (args != null)
            {
                foreach (var a in args)
                {
                    if (a == "--ui=wpf") return Wpf;
                    if (a == "--ui=winui") return WinUi;
                }
            }
            var saved = ReadSaved();
            if (saved != null) return saved;
            return Environment.OSVersion.Version.Build >= 22000 ? WinUi : Wpf;
        }

        public static string? ReadSaved()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                    var ui = doc.RootElement.TryGetProperty("ui", out var p) ? p.GetString() : null;
                    if (ui == Wpf || ui == WinUi) return ui;
                }
            }
            catch { }
            return null;
        }

        public static void Save(string ui)
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new { ui }));
            }
            catch { }
        }

        /// <summary>Saves the choice and restarts the launcher into the other UI.</summary>
        public static void SwitchTo(string ui)
        {
            Save(ui);
            var exe = Environment.ProcessPath;
            if (exe != null)
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            Environment.Exit(0);
        }
    }
}
