using Microsoft.Win32;

namespace OrangLauncher.Managers
{
    public static class StartupState
    {

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

        // register the .mrpack extension for the current user so double-clicking a file would open ts launcher not some called modrinth app :>>>

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
