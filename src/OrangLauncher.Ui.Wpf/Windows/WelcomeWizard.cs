using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrangLauncher.Managers;

namespace OrangLauncher
{
    public class WelcomeWizard : Window
    {
        private const int PageCount = 5;
        private int _page;
        private readonly bool[] _completed = { true, false, false, false, true };
        private bool _profileCreated;
        private readonly MainWindow _main;
        private readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(28, 6, 28, 4) };
        private readonly TextBlock _subtitle = new() { FontSize = 12, Margin = new Thickness(28, 2, 28, 0) };
        private readonly ContentControl _body = new() { Margin = new Thickness(28, 12, 28, 12) };
        private readonly Button _backBtn = new() { Padding = new Thickness(18, 8, 18, 8) };
        private readonly Button _nextBtn = new() { Padding = new Thickness(22, 8, 22, 8), FontWeight = FontWeights.Bold };
        private ComboBox? _javaCombo;
        private TextBox? _profileName = null;
        private ComboBox? _profileLoader, _profileVersion;
        private Slider? _profileRam;
        private CheckBox? _discordCheck, _telemetryCheck;
        private ComboBox? _themeCombo;
        private static string T(string key, string fallback) => LocalizationManager.GetString(key, fallback);

        public WelcomeWizard(MainWindow main)
        {
            _main = main;
            Owner = main;
            Title = "OrangLauncher";
            Width = 700; Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            SetResourceReference(BackgroundProperty, "BgPrimaryBrush");
            _subtitle.SetResourceReference(TextBlock.ForegroundProperty, "FgSecondaryBrush");
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var title = new TextBlock
            {
                Text = "OrangLauncher",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(28, 24, 28, 0)
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "AccentPrimaryBrush");
            Grid.SetRow(title, 0); root.Children.Add(title);
            Grid.SetRow(_subtitle, 1); root.Children.Add(_subtitle);
            for (int i = 0; i < PageCount; i++)
            {
                var dot = new TextBlock { Text = "●", FontSize = 15, Margin = new Thickness(0, 0, 8, 0) };
                _dots.Children.Add(dot);
            }
            Grid.SetRow(_dots, 2); root.Children.Add(_dots);
            Grid.SetRow(_body, 3); root.Children.Add(_body);

            var footer = new DockPanel { Margin = new Thickness(28, 0, 28, 22), LastChildFill = false };
            _backBtn.Content = T("WIZARD_BACK", "Back");
            _backBtn.Click += (s, e) => { if (_page > 0) { _page--; RenderPage(); } };
            DockPanel.SetDock(_backBtn, Dock.Left);
            _nextBtn.Click += (s, e) => GoNext();
            DockPanel.SetDock(_nextBtn, Dock.Right);
            footer.Children.Add(_backBtn);
            footer.Children.Add(_nextBtn);
            Grid.SetRow(footer, 4); root.Children.Add(footer);

            Content = root;
            RenderPage();
        }

        public static void ShowIfNeeded(MainWindow main)
        {
            if (SetupFlags.IsSetupDone()) return;
            new WelcomeWizard(main).ShowDialog();
        }

        private void SetComplete(int idx, bool value = true) { _completed[idx] = value; RefreshNav(); }

        private void RefreshNav()
        {
            for (int i = 0; i < PageCount; i++)
            {
                var dot = (TextBlock)_dots.Children[i];
                if (i == _page) dot.SetResourceReference(TextBlock.ForegroundProperty, "AccentPrimaryBrush");
                else if (_completed[i]) dot.SetResourceReference(TextBlock.ForegroundProperty, "FgSecondaryBrush");
                else dot.SetResourceReference(TextBlock.ForegroundProperty, "FgTertiaryBrush");
            }
            _backBtn.IsEnabled = _page > 0;
            _nextBtn.Content = _page == PageCount - 1 ? T("WIZARD_FINISH", "Finish") : T("WIZARD_CONTINUE", "Continue");
            _nextBtn.IsEnabled = _completed[_page];
        }

        private async void GoNext()
        {
            if (!_completed[_page]) return;
            if (_page == 3 && !_profileCreated && !await CreateProfileAsync()) return;
            if (_page == PageCount - 1) { Finish(); return; }
            _page++;
            RenderPage();
        }

        private void Finish()
        {
            ApplySettings();
            SetupFlags.MarkSetupDone();
            DialogResult = true;
            Close();
        }

        private void RenderPage()
        {
            _body.Content = _page switch
            {
                0 => BuildGreetPage(),
                1 => BuildAccountPage(),
                2 => BuildJavaPage(),
                3 => BuildProfilePage(),
                _ => BuildSettingsPage()
            };
            RefreshNav();
        }

        private StackPanel NewPage(string subtitleKey, string subtitleFallback, string headKey, string headFallback, string descKey, string descFallback)
        {
            _subtitle.Text = T(subtitleKey, subtitleFallback);
            var panel = new StackPanel();
            var head = new TextBlock { Text = T(headKey, headFallback), FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 4) };
            head.SetResourceReference(TextBlock.ForegroundProperty, "FgPrimaryBrush");
            panel.Children.Add(head);
            var desc = new TextBlock { Text = T(descKey, descFallback), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            desc.SetResourceReference(TextBlock.ForegroundProperty, "FgSecondaryBrush");
            panel.Children.Add(desc);
            return panel;
        }

        private UIElement BuildGreetPage()
        {
            var panel = NewPage("WIZARD_STEP_GREET", "Welcome", "WIZARD_GREET_TITLE", "Welcome to OrangLauncher!",
                "WIZARD_GREET_TAGLINE", "Let's get you set up in a few quick steps.");
            (string, string)[] steps =
            {
                (T("WIZARD_GREET_STEP_ACCOUNT", "Add your account"), T("WIZARD_GREET_STEP_ACCOUNT_DESC", "Sign in with Microsoft or play offline.")),
                (T("WIZARD_GREET_STEP_JAVA", "Pick a Java runtime"), T("WIZARD_GREET_STEP_JAVA_DESC", "We recommend the newest installed Java.")),
                (T("WIZARD_GREET_STEP_PROFILE", "Create your first profile"), T("WIZARD_GREET_STEP_PROFILE_DESC", "Choose a Minecraft version, loader and RAM.")),
                (T("WIZARD_GREET_STEP_SETTINGS", "Tune the launcher"), T("WIZARD_GREET_STEP_SETTINGS_DESC", "Theme, Discord presence and cleanup options."))
            };
            for (int i = 0; i < steps.Length; i++)
            {
                var row = new DockPanel { Margin = new Thickness(0, 7, 0, 7) };
                var num = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Margin = new Thickness(0, 0, 14, 0) };
                num.SetResourceReference(Border.BackgroundProperty, "AccentPrimaryBrush");
                num.Child = new TextBlock
                {
                    Text = (i + 1).ToString(), Foreground = Brushes.White, FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(num, Dock.Left);
                row.Children.Add(num);
                var info = new StackPanel();
                var t = new TextBlock { Text = steps[i].Item1, FontWeight = FontWeights.Bold };
                t.SetResourceReference(TextBlock.ForegroundProperty, "FgPrimaryBrush");
                var d = new TextBlock { Text = steps[i].Item2, FontSize = 11 };
                d.SetResourceReference(TextBlock.ForegroundProperty, "FgSecondaryBrush");
                info.Children.Add(t); info.Children.Add(d);
                row.Children.Add(info);
                panel.Children.Add(row);
            }
            var skip = new Button { Content = T("WIZARD_SKIP_ALL", "Skip setup"), Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            skip.Click += (s, e) => { SetupFlags.MarkSetupDone(); DialogResult = false; Close(); };
            panel.Children.Add(skip);
            return panel;
        }

        private UIElement BuildAccountPage()
        {
            var panel = NewPage("WIZARD_STEP_ACCOUNT", "Step 2 of 5 - Account", "WIZARD_ACCOUNT_TITLE", "Add your account",
                "WIZARD_ACCOUNT_DESC", "Sign in with your Microsoft account to play online, or add an offline account.");
            var status = new TextBlock { Margin = new Thickness(0, 0, 0, 12) };
            status.SetResourceReference(TextBlock.ForegroundProperty, "FgSecondaryBrush");
            void UpdateStatus()
            {
                var accounts = ProfileManager.Instance.GetProfiles();
                if (accounts.Count > 0)
                {
                    status.Text = string.Format(T("WIZARD_ACCOUNT_SIGNED_IN", "Signed in: {0}"), string.Join(", ", accounts.Select(a => a.Username)));
                    status.SetResourceReference(TextBlock.ForegroundProperty, "AccentPrimaryBrush");
                    SetComplete(1);
                }
                else
                {
                    status.Text = T("WIZARD_ACCOUNT_NO_ACCOUNTS", "No accounts yet.");
                }
            }
            UpdateStatus();
            panel.Children.Add(status);
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var ms = new Button { Content = T("WIZARD_ACCOUNT_LOGIN_MS", "Sign in with Microsoft"), Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 10, 0) };
            ms.Click += (s, e) =>
            {
                var dialog = new MicrosoftLoginDialog { Owner = this };
                if (dialog.ShowDialog() == true && dialog.ResultProfile != null)
                    ProfileManager.Instance.AddOrUpdateProfile(dialog.ResultProfile);
                UpdateStatus();
            };
            row.Children.Add(ms);
            var offline = new Button { Content = T("WIZARD_ACCOUNT_OFFLINE", "Add offline account"), Padding = new Thickness(18, 8, 18, 8), Margin = new Thickness(0, 0, 10, 0) };
            offline.Click += (s, e) =>
            {
                var dialog = new AddTextDialog(T("WIZARD_ACCOUNT_OFFLINE_PROMPT", "Enter your offline username:"), T("WIZARD_ACCOUNT_OFFLINE", "Add offline account")) { Owner = this };
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResultText))
                    ProfileManager.Instance.AddProfile(dialog.ResultText.Trim(), "Offline");
                UpdateStatus();
            };
            row.Children.Add(offline);
            var skip = new Button { Content = T("WIZARD_ACCOUNT_SKIP", "Skip for now"), Padding = new Thickness(18, 8, 18, 8) };
            skip.Click += (s, e) => { SetComplete(1); _page++; RenderPage(); };
            row.Children.Add(skip);
            panel.Children.Add(row);
            return panel;
        }

        private UIElement BuildJavaPage()
        {
            var panel = NewPage("WIZARD_STEP_JAVA", "Step 3 of 5 - Java", "WIZARD_JAVA_TITLE", "Pick a Java runtime",
                "WIZARD_JAVA_DESC", "Minecraft needs Java. \"Auto\" picks the right one per game version; missing runtimes are downloaded automatically.");
            _javaCombo = new ComboBox { Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
            _javaCombo.Items.Add(T("WIZARD_JAVA_AUTO", "Auto (Detect / download when needed)"));
            foreach (var ver in JavaManager.GetAvailableVersions())
            {
                var label = JavaManager.IsJavaInstalled(ver) ? $"Java {ver} ({T("WIZARD_JAVA_INSTALLED", "installed")})" : $"Java {ver}";
                _javaCombo.Items.Add(label);
            }
            _javaCombo.SelectedIndex = 0;
            panel.Children.Add(_javaCombo);
            SetComplete(2);
            return panel;
        }

        private UIElement BuildProfilePage()
        {
            var panel = NewPage("WIZARD_STEP_PROFILE", "Step 4 of 5 - First profile", "WIZARD_PROFILE_TITLE", "Create your first profile",
                "WIZARD_PROFILE_DESC", "You can add more profiles later from the Game Profiles tab.");
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 4; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            void AddLabel(int row, string text)
            {
                var l = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 10, 6) };
                l.SetResourceReference(TextBlock.ForegroundProperty, "FgPrimaryBrush");
                Grid.SetRow(l, row); Grid.SetColumn(l, 0); grid.Children.Add(l);
            }
            void AddField(int row, FrameworkElement el)
            {
                el.Margin = new Thickness(0, 6, 0, 6);
                Grid.SetRow(el, row); Grid.SetColumn(el, 1); grid.Children.Add(el);
            }

            AddLabel(0, T("GAME_PROFILES_NAME", "Name"));
            _profileName = new TextBox();
            var baseName = T("WIZARD_PROFILE_DEFAULT_NAME", "My Profile");
            var name = baseName; int n = 1;
            while (InstanceManager.Instance.GetInstanceByName(name) != null) name = $"{baseName} {++n}";
            _profileName.Text = name;
            _profileName.TextChanged += (s, e) => SetComplete(3, !string.IsNullOrWhiteSpace(_profileName.Text));
            AddField(0, _profileName);

            AddLabel(1, T("GAME_PROFILES_LOADER", "Mod Loader"));
            _profileLoader = new ComboBox();
            foreach (var l in new[] { "vanilla", "forge", "neoforge", "fabric", "quilt" }) _profileLoader.Items.Add(l);
            _profileLoader.SelectedIndex = 0;
            AddField(1, _profileLoader);

            AddLabel(2, T("GAME_PROFILES_VERSION", "Version"));
            _profileVersion = new ComboBox();
            AddField(2, _profileVersion);
            _ = LoadVersionsIntoComboAsync();

            AddLabel(3, T("GAME_PROFILES_RAM", "RAM"));
            var ramPanel = new DockPanel();
            var ramText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), MinWidth = 70 };
            ramText.SetResourceReference(TextBlock.ForegroundProperty, "FgSecondaryBrush");
            DockPanel.SetDock(ramText, Dock.Right);
            long totalMb = SystemPerformanceHelper.GetTotalRamMB();
            _profileRam = new Slider
            {
                Minimum = 1024,
                Maximum = Math.Max(2048, totalMb),
                Value = Math.Min(4096, Math.Max(2048, totalMb / 2)),
                TickFrequency = 512,
                IsSnapToTickEnabled = true
            };
            _profileRam.ValueChanged += (s, e) => ramText.Text = $"{(int)_profileRam.Value} MB";
            ramText.Text = $"{(int)_profileRam.Value} MB";
            ramPanel.Children.Add(ramText);
            ramPanel.Children.Add(_profileRam);
            AddField(3, ramPanel);

            panel.Children.Add(grid);
            SetComplete(3, !string.IsNullOrWhiteSpace(_profileName.Text));
            return panel;
        }

        private async Task LoadVersionsIntoComboAsync()
        {
            try
            {
                var versions = await GameProfileManager.Instance.GetVersions();
                if (_profileVersion == null) return;
                _profileVersion.ItemsSource = versions.Select(v => v.Id).ToList();
                if (_profileVersion.Items.Count > 0) _profileVersion.SelectedIndex = 0;
            }
            catch { }
        }

        private async Task<bool> CreateProfileAsync()
        {
            var name = _profileName?.Text?.Trim() ?? "";
            var version = _profileVersion?.SelectedItem?.ToString() ?? "";
            var loader = _profileLoader?.SelectedItem?.ToString() ?? "vanilla";
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
            {
                MessageBox.Show(T("WIZARD_PROFILE_MISSING", "Please enter a name and pick a version."), "OrangLauncher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (InstanceManager.Instance.GetInstanceByName(name) != null)
            {
                MessageBox.Show(string.Format(T("WIZARD_PROFILE_NAME_TAKEN", "A profile called '{0}' already exists."), name), "OrangLauncher", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            var installedVersionName = version;
            if (loader != "vanilla")
            {
                try
                {
                    _nextBtn.IsEnabled = false;
                    _subtitle.Text = string.Format(T("WIZARD_PROFILE_INSTALLING", "Installing {0} for {1}..."), loader, version);
                    installedVersionName = await ModLoaderInstaller.InstallLoaderAsync(PlatformPaths.GetMinecraftDir(), version, loader, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to install {loader}: {ex.Message}", "OrangLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                finally
                {
                    _nextBtn.IsEnabled = true;
                    RefreshNav();
                }
            }
            var ram = ((int)(_profileRam?.Value ?? 4096)).ToString();
            var instance = InstanceManager.Instance.CreateInstance(name, installedVersionName, loader, ram);
            if (instance != null)
            {
                instance.Version = installedVersionName;
                instance.InstalledVersionName = installedVersionName;
                InstanceManager.Instance.SaveInstances();
            }
            _profileCreated = instance != null;
            return _profileCreated;
        }

        private UIElement BuildSettingsPage()
        {
            var panel = NewPage("WIZARD_STEP_SETTINGS", "Step 5 of 5 - Settings", "WIZARD_SETTINGS_TITLE", "Recommended settings",
                "WIZARD_SETTINGS_DESC", "You can change all of these later in Settings.");
            _discordCheck = new CheckBox { Content = T("WIZARD_SETTINGS_DISCORD", "Enable Discord Rich Presence"), IsChecked = true, Margin = new Thickness(0, 5, 0, 5) };
            _telemetryCheck = new CheckBox { Content = T("WIZARD_SETTINGS_TELEMETRY", "Delete Minecraft telemetry on startup"), IsChecked = true, Margin = new Thickness(0, 5, 0, 5) };
            _discordCheck.SetResourceReference(ForegroundProperty, "FgPrimaryBrush");
            _telemetryCheck.SetResourceReference(ForegroundProperty, "FgPrimaryBrush");
            panel.Children.Add(_discordCheck);
            panel.Children.Add(_telemetryCheck);
            var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var themeLabel = new TextBlock { Text = T("WIZARD_SETTINGS_THEME", "Theme"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            themeLabel.SetResourceReference(TextBlock.ForegroundProperty, "FgPrimaryBrush");
            themeRow.Children.Add(themeLabel);
            _themeCombo = new ComboBox { Width = 200 };
            _themeCombo.Items.Add(new ComboBoxItem { Content = "Arc Dark", Tag = "arc" });
            _themeCombo.Items.Add(new ComboBoxItem { Content = "Dark Prism", Tag = "dark_prism" });
            _themeCombo.Items.Add(new ComboBoxItem { Content = "Light Mode", Tag = "light" });
            _themeCombo.SelectedIndex = 0;
            themeRow.Children.Add(_themeCombo);
            panel.Children.Add(themeRow);
            var hint = new TextBlock
            {
                Text = T("WIZARD_SETTINGS_FINISH_HINT", "Press Finish and you're ready to play!"),
                FontStyle = FontStyles.Italic, FontSize = 11, Margin = new Thickness(0, 14, 0, 0)
            };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "FgSecondaryBrush");
            panel.Children.Add(hint);
            SetComplete(4);
            return panel;
        }

        private void ApplySettings()
        {
            try
            {
                _main.DiscordRpcCheckBox.IsChecked = _discordCheck?.IsChecked ?? true;
                _main.DeleteTelemetryCheckBox.IsChecked = _telemetryCheck?.IsChecked ?? false;
                if (_themeCombo?.SelectedItem is ComboBoxItem item && item.Tag is string themeKey)
                {
                    foreach (ComboBoxItem t in _main.ThemeComboBox.Items)
                        if (t.Tag?.ToString() == themeKey) { _main.ThemeComboBox.SelectedItem = t; break; }
                    ThemeManager.ApplyTheme(themeKey);
                }
            }
            catch { }
        }
    }
}