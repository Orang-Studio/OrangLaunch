using Microsoft.Win32;

namespace OrangLauncher.Managers
{
    /// <summary>
    /// Command-line state captured by the host before a UI starts, plus the
    /// per-user .mrpack file association.
    /// </summary>
    public static class StartupState
    {
        /// <summary>A .mrpack file the launcher was opened with, waiting to be imported.</summary>
        public static string? PendingMrpackPath { get; set; }

        public static void CaptureArgs(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase) && File.Exists(arg))
                {
                    PendingMrpackPath = Path.GetFullPath(arg);
                    break;
                }
            }
        }

        /// <summary>
        /// Registers the .mrpack extension for the current user so double-clicking a
        /// modpack opens it with the launcher. Safe to call on every startup.
        /// </summary>
        public static void RegisterMrpackFileAssociation()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;
                using var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
                if (classes == null) return;

                using (var ext = classes.CreateSubKey(".mrpack"))
                    ext.SetValue("", "OrangLauncher.mrpack");
                using (var prog = classes.CreateSubKey("OrangLauncher.mrpack"))
                {
                    prog.SetValue("", "Modrinth Modpack");
                    using (var icon = prog.CreateSubKey("DefaultIcon"))
                        icon.SetValue("", $"\"{exePath}\",0");
                    using (var cmd = prog.CreateSubKey(@"shell\open\command"))
                        cmd.SetValue("", $"\"{exePath}\" \"%1\"");
                }
            }
            catch { }
        }
    }
}
