using Microsoft.UI.Xaml;
using OrangLauncher.Managers;
namespace OrangLauncher.Ui.WinUI
{
    public static class EntryPoint
    {
        public static void Run()
        {
            LocalizationManager.Variant = "winui";
            FilePicker.PickMultiple = async (title, extensions) =>
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                var window = App.MainAppWindow;
                if (window == null) return null;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                foreach (var ext in extensions) picker.FileTypeFilter.Add(ext);
                picker.FileTypeFilter.Add("*");
                var files = await picker.PickMultipleFilesAsync();
                return files?.Select(f => f.Path).ToArray();
            };
            Application.Start(_ =>
            {
                var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(dq));
                new App();
            });
        }
    }
}