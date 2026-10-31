using System.IO.Pipes;
using Microsoft.Win32;

namespace OrangLauncher.Managers
{
    public static class StartupState
    {
        private const string PipeName = "OrangLauncher.OpenFile";
        private static readonly string[] PackExtensions = [".mrpack", ".orangpack", ".zip"];
        private static Mutex? _instanceMutex;
        public static string? PendingMrpackPath { get; set; }
        public static event Action<string>? FileReceived;

        public static void CaptureArgs(string[] args)
        {
            foreach (var arg in args)
            {
                if (PackExtensions.Any(e => arg.EndsWith(e, StringComparison.OrdinalIgnoreCase)) && File.Exists(arg))
                {
                    PendingMrpackPath = Path.GetFullPath(arg);
                    break;
                }
            }
        }

        public static bool TryHandOffToRunningInstance()
        {
            _instanceMutex = new Mutex(true, "OrangLauncher.SingleInstance", out var created);
            if (created || PendingMrpackPath == null) return false;
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(3000);
                using var writer = new StreamWriter(client);
                writer.Write(PendingMrpackPath);
                writer.Flush();
                return true;
            }
            catch { return false; }
        }

        public static void StartListening()
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1);
                        server.WaitForConnection();
                        using var reader = new StreamReader(server);
                        var path = reader.ReadToEnd().Trim();
                        if (File.Exists(path)) FileReceived?.Invoke(path);
                    }
                    catch
                    {
                        Thread.Sleep(1000);
                    }
                }
            }) { IsBackground = true, Name = "OpenFilePipe" };
            thread.Start();
        }

        public static void RegisterMrpackFileAssociation()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;
                using var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
                if (classes == null) return;
                foreach (var (ext, progId, name) in new[] { (".mrpack", "OrangLauncher.mrpack", "Modrinth Modpack"), (".orangpack", "OrangLauncher.orangpack", "OrangLauncher Pack") })
                {
                    using (var key = classes.CreateSubKey(ext))
                        key.SetValue("", progId);
                    using var prog = classes.CreateSubKey(progId);
                    prog.SetValue("", name);
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
