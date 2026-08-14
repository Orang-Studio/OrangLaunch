using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using OrangLauncher.Managers;
using OrangLauncher.Models;
namespace OrangLauncher
{
    public partial class MicrosoftLoginDialog : Window
    {
        public UserProfile? ResultProfile { get; private set; }
        private readonly MicrosoftAuthManager _authManager;
        private bool _isProcessing = false;
        public MicrosoftLoginDialog()
        {
            InitializeComponent();
            Managers.WebView2Helper.Prepare(LoginWebView);
            _authManager = new MicrosoftAuthManager();
            Loaded += MicrosoftLoginDialog_Loaded;
        }
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
                // when WebView2 runtime missing/broken: fall back to the system browser and
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
                var prompt = LocalizationManager.GetString("LOGIN_BROWSER_FALLBACK",
                    "WebView2 is not available, so your browser was opened instead.\n" +
                    "1. Sign in with your Microsoft account in the browser.\n" +
                    "2. When you land on a blank page, copy its full address IMMEDIATELY -\n" +
                    "    the code is removed from the address a moment after the page loads.\n" +
                    "3. Paste that address (or just the code=... value) below.");
                var dialog = new AddTextDialog(prompt,
                    LocalizationManager.GetString("LOGIN_BROWSER_TITLE", "Sign in with your browser")) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    var code = MicrosoftAuthManager.TryExtractCodeFromRedirect(dialog.ResultText ?? "");
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
                        DialogResult = true;
                        Close();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Browser login failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            DialogResult = false;
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
                        DialogResult = true;
                    }
                    else
                    {
                        var error = query["error"];
                        var errorDescription = query["error_description"];
                        MessageBox.Show($"Login failed: {error}\n{errorDescription}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        DialogResult = false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Authentication failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                }
                Close();
            }
        }
    }
}