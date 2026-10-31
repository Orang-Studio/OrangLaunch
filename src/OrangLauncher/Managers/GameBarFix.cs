using Microsoft.Win32;
namespace OrangLauncher.Managers
{
    public static class GameBarFix
    {
        public const string NoopArg = "--ms-gamingoverlay-noop";
        private const string HandlerKey = @"Software\Classes\ms-gamingoverlay";
        private const string GameDvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        private const string GameConfigKey = @"System\GameConfigStore";
        public static bool Enabled { get => LauncherSettings.GetBool("suppressGameBarPopup", false); set => LauncherSettings.Set("suppressGameBarPopup", value); }
        public static bool IsNoopLaunch(string[] args) => args.Any(a => a == NoopArg || a.StartsWith("ms-gamingoverlay:", StringComparison.OrdinalIgnoreCase));
        public static void Apply(bool enable)
        {
            try
            {
                using (var dvr = Registry.CurrentUser.CreateSubKey(GameDvrKey))
                    dvr.SetValue("AppCaptureEnabled", enable ? 0 : 1, RegistryValueKind.DWord);
                using (var store = Registry.CurrentUser.CreateSubKey(GameConfigKey))
                    store.SetValue("GameDVR_Enabled", enable ? 0 : 1, RegistryValueKind.DWord);
                if (enable)
                {
                    var exe = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exe)) return;
                    using var key = Registry.CurrentUser.CreateSubKey(HandlerKey);
                    key.SetValue("", "URL:ms-gamingoverlay");
                    key.SetValue("URL Protocol", "");
                    key.SetValue("OrangLauncher", "1");
                    using var cmd = key.CreateSubKey(@"shell\open\command");
                    cmd.SetValue("", $"\"{exe}\" {NoopArg}");
                }
                else
                {
                    using var key = Registry.CurrentUser.OpenSubKey(HandlerKey);
                    var ours = key?.GetValue("OrangLauncher") != null;
                    key?.Dispose();
                    if (ours) Registry.CurrentUser.DeleteSubKeyTree(HandlerKey, false);
                }
            }
            catch { }
        }
    }
}