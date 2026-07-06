using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
namespace OrangLauncher
{
    public partial class MicrosoftLoginWindow : Window
    {
        private readonly string _loginUrl;
        private readonly string _redirectUri;
        private readonly TaskCompletionSource<string> _authCodeTcs;
        public MicrosoftLoginWindow(string loginUrl, string redirectUri)
        {
            InitializeComponent();
            Managers.WebView2Helper.Prepare(LoginWebView);
            _loginUrl = loginUrl;
            _redirectUri = redirectUri;
            _authCodeTcs = new TaskCompletionSource<string>();
            Loaded += MicrosoftLoginWindow_Loaded;
        }
        public Task<string> GetAuthCodeAsync()
        {
            return _authCodeTcs.Task;
        }
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
            await LoginWebView.EnsureCoreWebView2Async(null);
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
                MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ProcessUrlButton_Click(object sender, RoutedEventArgs e)
        {
            var url = ClipboardUrlTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Please paste the redirect URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        MessageBox.Show("Could not extract authorization code from URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Invalid redirect URL. Please copy the complete URL from your browser.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process URL: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _authCodeTcs.TrySetCanceled();
            Close();
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _authCodeTcs.TrySetCanceled();
        }
    }
}