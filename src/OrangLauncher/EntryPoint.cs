using System.IO;
using System.Windows;
using OrangLauncher.Managers;
namespace OrangLauncher
{
    public static class EntryPoint
    {
        private static void WriteCrash(string kind, Exception ex)
        {
            try
            {
                var path = Path.Combine(LaunchService.LogsDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.AppendAllText(path, $"[{kind}] {DateTime.Now:O}\r\nOrangLauncher {UpdateManager.GetCurrentVersionString()}\r\n{ex}\r\n");
            }
            catch { }
        }
        public static void Run()
        {
            LauncherSettings.Reload();
            LocalizationManager.LoadLanguage(LauncherSettings.Language);
            NetworkProxy.Apply();
            AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) WriteCrash("fatal", ex); };
            TaskScheduler.UnobservedTaskException += (_, e) => { WriteCrash("task", e.Exception); e.SetObserved(); };
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (s, e) =>
            {
                WriteCrash("ui", e.Exception);
                try
                {
                    UiKit.Error(LocalizationManager.GetString("ERROR", "Error"), e.Exception.Message);
                }
                catch { }
                e.Handled = true;
            };
            ThemeManager.ApplyTheme(LauncherSettings.Theme);
            ThemeManager.ApplyAccent(LauncherSettings.AccentColor);
            ThemeManager.EnsureControls();
            var window = new MainWindow();
            app.Run(window);
}   }   }