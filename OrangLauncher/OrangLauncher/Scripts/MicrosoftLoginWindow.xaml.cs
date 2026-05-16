using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI;
namespace OrangLauncher
{
    public sealed partial class MicrosoftLoginWindow : Window
    {
        private readonly string _loginUrl;
        private readonly string _redirectUri;
        private readonly TaskCompletionSource<string> _authCodeTcs;
        public MicrosoftLoginWindow(string loginUrl, string redirectUri)
        {
            this.InitializeComponent();
            _loginUrl = loginUrl;
            _redirectUri = redirectUri;
            _authCodeTcs = new TaskCompletionSource<string>();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow.GetFromWindowId(windowId)?.Resize(new Windows.Graphics.SizeInt32(500, 700));
            if (Content is FrameworkElement fe)
                fe.Loaded += MicrosoftLoginWindow_Loaded;
        }
        public Task<string> GetAuthCodeAsync() => _authCodeTcs.Task;
        private async void MicrosoftLoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeWebViewAsync();
            }
            catch (Exception ex)
            {
                LoginWebView.Visibility = Visibility.Collapsed;
                FallbackPanel.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
            }
        }
        private async Task InitializeWebViewAsync()
        {
            await LoginWebView.EnsureCoreWebView2Async();
            LoginWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            LoginWebView.Source = new Uri(_loginUrl);
        }
        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri.StartsWith(_redirectUri, StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(e.Uri);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var code = query.Get("code");
                if (!string.IsNullOrEmpty(code))
                {
                    _authCodeTcs.TrySetResult(code);
                    Close();
                }
                else
                {
                    var error = query.Get("error");
                    var errorDescription = query.Get("error_description");
                    _authCodeTcs.TrySetException(new Exception($"Login failed: {error} - {errorDescription}"));
                    Close();
                }
                e.Cancel = true;
            }
        }
        private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _loginUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowMessageAsync($"Failed to open browser: {ex.Message}");
            }
        }
        private void ProcessUrlButton_Click(object sender, RoutedEventArgs e)
        {
            var url = ClipboardUrlTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                ShowMessageAsync("Please paste the redirect URL.");
                return;
            }
            try
            {
                if (url.StartsWith(_redirectUri, StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(url);
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var code = query.Get("code");
                    if (!string.IsNullOrEmpty(code))
                    {
                        LoadingPanel.Visibility = Visibility.Visible;
                        FallbackPanel.Visibility = Visibility.Collapsed;
                        _authCodeTcs.TrySetResult(code);
                        Close();
                    }
                    else
                    {
                        ShowMessageAsync("Could not extract authorization code from URL.");
                    }
                }
                else
                {
                    ShowMessageAsync("Invalid redirect URL. Please copy the complete URL from your browser.");
                }
            }
            catch (Exception ex)
            {
                ShowMessageAsync($"Failed to process URL: {ex.Message}");
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _authCodeTcs.TrySetCanceled();
            Close();
        }
        private async void ShowMessageAsync(string message)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "OrangLauncher",
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch { }
        }
    }
}