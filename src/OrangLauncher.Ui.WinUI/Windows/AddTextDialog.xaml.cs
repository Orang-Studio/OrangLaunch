using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI;
namespace OrangLauncher
{
    public sealed partial class AddTextDialog : Window
    {
        public string? ResultText { get; private set; }
        public bool Confirmed { get; private set; }
        private readonly TaskCompletionSource<bool> _tcs = new();
        public AddTextDialog(string prompt, string title, string defaultValue = "")
        {
            this.InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputTextBox.Text = defaultValue;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow?.Resize(new Windows.Graphics.SizeInt32(350, 200));
        }
        public Task<bool> WaitForResultAsync() => _tcs.Task;
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText = InputTextBox.Text;
            Confirmed = true;
            _tcs.TrySetResult(true);
            Close();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            _tcs.TrySetResult(false);
            Close();
        }
    }
}