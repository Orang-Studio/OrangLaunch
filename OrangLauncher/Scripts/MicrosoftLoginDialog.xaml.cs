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
                MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
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