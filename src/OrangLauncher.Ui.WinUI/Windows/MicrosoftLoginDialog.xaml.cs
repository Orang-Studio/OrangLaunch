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
                var env = await WebView2Helper.GetEnvironmentAsync();
                await LoginWebView.EnsureCoreWebView2Async(env);
                LoginWebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                string authUrl = _authManager.GetLoginUrl();
                LoginWebView.CoreWebView2.Navigate(authUrl);
            }
            catch (Exception ex)
            {
                // WebView2 runtime missing/broken: fall back to the system browser and
                // let the user paste the redirect URL that contains the auth code.
                System.Diagnostics.Debug.WriteLine($"WebView2 init failed, using browser fallback: {ex.Message}");
                await RunBrowserFallbackAsync();
            }
        }
        private async Task RunBrowserFallbackAsync()
        {
            try
            {
                var authUrl = _authManager.GetLoginUrl();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = authUrl, UseShellExecute = true });
                var input = new Microsoft.UI.Xaml.Controls.TextBox
                {
                    PlaceholderText = "https://login.live.com/oauth20_desktop.srf?code=...",
                    AcceptsReturn = false
                };
                var panel = new Microsoft.UI.Xaml.Controls.StackPanel { Spacing = 8 };
                panel.Children.Add(new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = LocalizationManager.GetString("LOGIN_BROWSER_FALLBACK",
                        "WebView2 is not available, so your browser was opened instead.\n" +
                        "1. Sign in with your Microsoft account in the browser.\n" +
                        "2. When you land on a blank page, copy its full address IMMEDIATELY -\n" +
                        "    the code is removed from the address a moment after the page loads.\n" +
                        "3. Paste that address (or just the code=... value) below."),
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                });
                panel.Children.Add(input);
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = LocalizationManager.GetString("LOGIN_BROWSER_TITLE", "Sign in with your browser"),
                    Content = panel,
                    PrimaryButtonText = LocalizationManager.GetString("LOGIN", "Log in"),
                    CloseButtonText = LocalizationManager.GetString("CANCEL", "Cancel"),
                    XamlRoot = Content.XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    var code = MicrosoftAuthManager.TryExtractCodeFromRedirect(input.Text);
                    if (!string.IsNullOrEmpty(code))
                    {
                        var auth = await _authManager.AuthenticateWithCodeAsync(code);
                        ResultProfile = new UserProfile
                        {
                            Username = auth.Username,
                            Uuid = auth.Uuid,
                            AccessToken = auth.AccessToken,
                            RefreshToken = auth.RefreshToken,
                            MinecraftToken = auth.AccessToken,
                            Type = "Microsoft",
                            LastUsed = DateTime.Now
                        };
                        Confirmed = true;
                        _tcs.TrySetResult(true);
                        Close();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Browser fallback login failed: {ex.Message}");
            }
            Confirmed = false;
            _tcs.TrySetResult(false);
            Close();
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