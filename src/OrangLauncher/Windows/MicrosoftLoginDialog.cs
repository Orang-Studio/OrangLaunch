using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OrangLauncher.Managers;
using OrangLauncher.Models;
using static OrangLauncher.UiKit;
namespace OrangLauncher
{
    public class MicrosoftLoginDialog : OlWindow
    {
        public UserProfile? ResultProfile { get; private set; }
        public string? Error { get; private set; }
        private readonly MicrosoftAuthManager _auth = new();
        private readonly Grid _host = new();
        private WebView2? _web;
        private bool _processing;
        private int _pollToken;
        private MicrosoftAuthManager.DeviceCodeInfo? _device;
        private DateTime _deviceStarted;
        private TextBlock? _code;
        private TextBlock? _deviceStatus;
        public MicrosoftLoginDialog(string mode = "embedded") : base(T("QT_MS_LOGIN_TITLE", "Sign in with Microsoft"), 560, 720)
        {
            var root = new DockPanel();
            var bar = new Border { Padding = new Thickness(12, 8, 12, 8), BorderThickness = new Thickness(0, 1, 0, 0) };
            bar.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            DockPanel.SetDock(bar, Dock.Bottom);
            var row = new DockPanel();
            var cancel = Button(T("CANCEL", "Cancel"), Close);
            DockPanel.SetDock(cancel, Dock.Right);
            row.Children.Add(cancel);
            var switchBtn = Button(mode == "browser" ? T("QT_MS_USE_EMBEDDED", "Use the embedded window instead") : T("QT_MS_BROWSER_OPTION", "Microsoft via browser"),
                null, "flat", "microsoft");
            switchBtn.HorizontalAlignment = HorizontalAlignment.Left;
            switchBtn.Click += (_, _) =>
            {
                if (_web != null) UseBrowser(); else UseEmbedded();
                SetButtonContent(switchBtn, _web != null ? T("QT_MS_BROWSER_OPTION", "Microsoft via browser") : T("QT_MS_USE_EMBEDDED", "Use the embedded window instead"), "microsoft");
            };
            row.Children.Add(switchBtn);
            bar.Child = row;
            root.Children.Add(bar);
            root.Children.Add(_host);
            Content = root;
            Loaded += (_, _) => { if (mode == "browser") UseBrowser(); else UseEmbedded(); };
            Closed += (_, _) => { _pollToken++; DropWeb(); };
        }
        private void DropWeb()
        {
            if (_web == null) return;
            try { _web.Dispose(); } catch { }
            _web = null;
        }
        private async void UseEmbedded()
        {
            _pollToken++;
            _host.Children.Clear();
            var web = new WebView2();
            WebView2Helper.Prepare(web);
            _web = web;
            _host.Children.Add(web);
            try
            {
                await web.EnsureCoreWebView2Async();
                web.CoreWebView2.NavigationStarting += OnNavigation;
                web.CoreWebView2.Navigate(_auth.GetLoginUrl());
            }
            catch (Exception ex)
            {
                Log($"embedded browser unavailable: {ex.Message}");
                UseBrowser();
            }
        }
        private static void Log(string m) => System.Diagnostics.Debug.WriteLine("[Accounts] " + m);
        private async void OnNavigation(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (_processing || !e.Uri.StartsWith(_auth.GetRedirectUri(), StringComparison.OrdinalIgnoreCase)) return;
            _processing = true;
            e.Cancel = true;
            var code = MicrosoftAuthManager.TryExtractCodeFromRedirect(e.Uri);
            if (string.IsNullOrEmpty(code))
            {
                _processing = false;
                return;
            }
            _host.Children.Clear();
            DropWeb();
            _host.Children.Add(Center(T("QT_MS_SIGNING_IN", "Signing in with Microsoft...")));
            try
            {
                ResultProfile = MicrosoftAuthManager.ToProfile(await _auth.AuthenticateWithCodeAsync(code));
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                DialogResult = false;
            }
        }
        private static FrameworkElement Center(string text)
        {
            var t = Label(text, "h3", true);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.TextAlignment = TextAlignment.Center;
            t.Margin = new Thickness(30);
            return t;
        }
        private string VerificationUrl() => _device == null ? "https://www.microsoft.com/link" : $"https://www.microsoft.com/link?otc={_device.UserCode}";
        private void UseBrowser()
        {
            DropWeb();
            _host.Children.Clear();
            var sp = new StackPanel { Margin = new Thickness(30), VerticalAlignment = VerticalAlignment.Center };
            var title = Label(T("QT_MS_DEVICE_TITLE", "Sign in with your browser"), "h2");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(title);
            var hint = Label(T("QT_MS_DEVICE_HINT", "Your browser opens microsoft.com/link. Enter this code there and approve the Minecraft sign-in; the launcher finishes by itself."), "muted", true);
            hint.TextAlignment = TextAlignment.Center;
            hint.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(hint);
            _code = new TextBlock { Text = "...", FontSize = 34, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 6), FontFamily = new FontFamily("Consolas") };
            _code.SetResourceReference(TextBlock.ForegroundProperty, "AccentPrimaryBrush");
            sp.Children.Add(_code);
            var buttons = Row(8,
                Button(T("QT_COPY", "Copy"), () => { if (_device != null) Copy(_device.UserCode); }),
                Button(T("QT_MS_OPEN_BROWSER", "Open browser"), () => OpenUrl(VerificationUrl()), "accent"));
            buttons.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(buttons);
            _deviceStatus = Label("", "muted", true);
            _deviceStatus.TextAlignment = TextAlignment.Center;
            _deviceStatus.Margin = new Thickness(0, 18, 0, 0);
            sp.Children.Add(_deviceStatus);
            _host.Children.Add(sp);
            StartDeviceFlow();
        }
        private async void StartDeviceFlow()
        {
            var token = ++_pollToken;
            if (_deviceStatus != null) _deviceStatus.Text = T("QT_MS_DEVICE_REQUESTING", "Requesting a sign-in code...");
            try
            {
                var data = await _auth.StartDeviceCodeAsync();
                if (token != _pollToken) return;
                _device = data;
                _deviceStarted = DateTime.Now;
                if (_code != null) _code.Text = data.UserCode;
                Copy(data.UserCode);
                if (_deviceStatus != null) _deviceStatus.Text = T("QT_MS_DEVICE_WAITING", "Waiting for you to sign in...");
                OpenUrl(VerificationUrl());
                Schedule(token, data.Interval);
            }
            catch (Exception ex)
            {
                if (token == _pollToken && _deviceStatus != null)
                    _deviceStatus.Text = T("QT_MS_DEVICE_FAIL", "Could not start the browser sign-in: {error}", ("error", ex.Message));
            }
        }
        private void Schedule(int token, int seconds)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(2, seconds)) };
            timer.Tick += (_, _) => { timer.Stop(); Poll(token); };
            timer.Start();
        }
        private async void Poll(int token)
        {
            if (token != _pollToken || _device == null || !IsVisible) return;
            try
            {
                var (state, result, error) = await _auth.PollDeviceCodeAsync(_device.DeviceCode);
                if (token != _pollToken) return;
                if (state == "ok" && result != null)
                {
                    ResultProfile = MicrosoftAuthManager.ToProfile(result);
                    DialogResult = true;
                    return;
                }
                if (state is "pending" or "slow")
                {
                    if (DateTime.Now - _deviceStarted > TimeSpan.FromSeconds(20) && _deviceStatus != null)
                        _deviceStatus.Text = T("QT_MS_DEVICE_STILL_WAITING", "Still waiting. If the browser ended up on account.microsoft.com without asking about the code, open https://www.microsoft.com/link again, type the code above and confirm the Minecraft sign-in.");
                    Schedule(token, _device.Interval + (state == "slow" ? 5 : 0));
                    return;
                }
                if (_deviceStatus != null) _deviceStatus.Text = T("QT_MS_DEVICE_FAIL", "Could not start the browser sign-in: {error}", ("error", error));
                if (error != null && error.Contains("expired")) StartDeviceFlow();
            }
            catch
            {
                Schedule(token, 10);
}   }   }   }