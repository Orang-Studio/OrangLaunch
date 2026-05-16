using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using OrangLauncher.Managers;
using OrangLauncher.Models;
namespace OrangLauncher
{
    public sealed partial class MicrosoftLoginDialog : Window
    {
        public UserProfile? ResultProfile { get; private set; }
        public bool Confirmed { get; private set; }
        private readonly MicrosoftAuthManager _authManager;
        private bool _isProcessing = false;
        private readonly TaskCompletionSource<bool> _tcs = new();
        public MicrosoftLoginDialog()
        {
            this.InitializeComponent();
            _authManager = new MicrosoftAuthManager();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow.GetFromWindowId(windowId)?.Resize(new Windows.Graphics.SizeInt32(500, 600));
            this.Closed += (s, e) => _tcs.TrySetResult(false);
            if (Content is FrameworkElement fe)
                fe.Loaded += MicrosoftLoginDialog_Loaded;
        }
        public Task<bool> WaitForResultAsync() => _tcs.Task;
        private async void MicrosoftLoginDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoginWebView.EnsureCoreWebView2Async();
                LoginWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                string authUrl = _authManager.GetLoginUrl();
                LoginWebView.CoreWebView2.Navigate(authUrl);
            }
            catch (Exception ex)
            {
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to initialize WebView2: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
                Confirmed = false;
                _tcs.TrySetResult(false);
                Close();
            }
        }
        private async void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (_isProcessing) return;
            if (e.Uri.StartsWith(_authManager.GetRedirectUri(), StringComparison.OrdinalIgnoreCase))
            {
                _isProcessing = true;
                e.Cancel = true;
                try
                {
                    var uri = new Uri(e.Uri);
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    var code = query["code"];
                    if (!string.IsNullOrEmpty(code))
                    {
                        var result = await _authManager.AuthenticateWithCodeAsync(code);
                        ResultProfile = new UserProfile
                        {
                            Username = result.Username,
                            Uuid = result.Uuid,
                            AccessToken = result.AccessToken,
                            RefreshToken = result.RefreshToken,
                            MinecraftToken = result.AccessToken,
                            Type = "Microsoft",
                            LastUsed = DateTime.Now
                        };
                        Confirmed = true;
                        _tcs.TrySetResult(true);
                    }
                    else
                    {
                        var error = query["error"];
                        var errorDescription = query["error_description"];
                        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                        {
                            Title = "Error",
                            Content = $"Login failed: {error}\n{errorDescription}",
                            CloseButtonText = "OK",
                            XamlRoot = Content.XamlRoot
                        };
                        await dialog.ShowAsync();
                        Confirmed = false;
                        _tcs.TrySetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                    {
                        Title = "Error",
                        Content = $"Authentication failed: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await dialog.ShowAsync();
                    Confirmed = false;
                    _tcs.TrySetResult(false);
                }
                Close();
            }
        }
    }
}