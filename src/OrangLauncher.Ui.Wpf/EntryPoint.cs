using System.IO;
using OrangLauncher.Managers;
namespace OrangLauncher.Ui.Wpf
{
    public static class EntryPoint
    {
        public static void Run()
        {
            LocalizationManager.Variant = "wpf";
            FilePicker.PickMultiple = (title, extensions) =>
            {
                var filterExts = string.Join(";", extensions.Select(e => "*" + e));
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = title,
                    Filter = $"Supported Files ({filterExts})|{filterExts}|All Files (*.*)|*.*",
                    Multiselect = true
                };
                return Task.FromResult<string[]?>(dialog.ShowDialog() == true ? dialog.FileNames : null);
            };
            var app = new System.Windows.Application();
            app.DispatcherUnhandledException += (s, e) =>
            {
                // Keep the launcher alive on recoverable UI errors (dialogs, update
                // checks, network hiccups) instead of tearing the process down.
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "oranglauncher-wpf.log"),
                        $"{DateTime.Now:HH:mm:ss.fff} UNHANDLED: {e.Exception}\r\n");
                    System.Windows.MessageBox.Show(e.Exception.Message, "OrangLauncher error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                catch { }
                e.Handled = true;
            };
            app.Run(new MainWindow());
        }
    }
}
