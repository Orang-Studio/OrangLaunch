using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OrangLauncher.Managers;

namespace OrangLauncher
{
    public partial class MainWindow
    {
        private const int WizardPageCount = 5;
        private int _wizPage;
        private readonly bool[] _wizCompleted = { true, false, false, false, true };
        private bool _wizProfileCreated;
        private Grid? _wizOverlay;
        private StackPanel? _wizDots;
        private TextBlock? _wizSubtitle;
        private ContentControl? _wizBody;
        private Button? _wizBackBtn, _wizNextBtn;
        private ComboBox? _wizJavaCombo, _wizLoaderCombo, _wizVersionCombo, _wizThemeCombo;
        private TextBox? _wizNameBox;
        private Slider? _wizRamSlider;
        private CheckBox? _wizDiscordCheck, _wizTelemetryCheck;

        private static string TW(string key, string fallback) => LocalizationManager.GetString(key, fallback);

        private void ShowWelcomeWizardIfNeeded()
        {
            if (SetupFlags.IsSetupDone() || _wizOverlay != null) return;
            _wizOverlay = new Grid();
            _wizOverlay.SetValue(Grid.RowSpanProperty, 99);
            _wizOverlay.SetValue(Grid.ColumnSpanProperty, 99);
            var themeRes = RootGrid.ActualTheme == ElementTheme.Light ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
            _wizOverlay.Background = new SolidColorBrush(themeRes);

            var root = new Grid { MaxWidth = 720, Margin = new Thickness(24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock { Text = "OrangLauncher", FontSize = 26, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
            Grid.SetRow(title, 0); root.Children.Add(title);
            _wizSubtitle = new TextBlock { FontSize = 13, Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0) };
            Grid.SetRow(_wizSubtitle, 1); root.Children.Add(_wizSubtitle);

            _wizDots = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 4), Spacing = 8 };
            for (int i = 0; i < WizardPageCount; i++)
                _wizDots.Children.Add(new TextBlock { Text = "●", FontSize = 15, Opacity = 0.35 });
            Grid.SetRow(_wizDots, 2); root.Children.Add(_wizDots);

            _wizBody = new ContentControl { Margin = new Thickness(0, 12, 0, 12), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetRow(_wizBody, 3); root.Children.Add(_wizBody);

            var footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _wizBackBtn = new Button { Content = TW("WIZARD_BACK", "Back"), Padding = new Thickness(18, 8, 18, 8) };
            _wizBackBtn.Click += (s, e) => { if (_wizPage > 0) { _wizPage--; WizRenderPage(); } };
            Grid.SetColumn(_wizBackBtn, 0); footer.Children.Add(_wizBackBtn);
            _wizNextBtn = new Button { Padding = new Thickness(22, 8, 22, 8), Style = Application.Current.Resources["AccentButtonStyle"] as Style };
            _wizNextBtn.Click += (s, e) => WizGoNext();
            Grid.SetColumn(_wizNextBtn, 2); footer.Children.Add(_wizNextBtn);
            Grid.SetRow(footer, 4); root.Children.Add(footer);

            var scroll = new ScrollViewer { Content = root, HorizontalAlignment = HorizontalAlignment.Center };
            _wizOverlay.Children.Add(scroll);
            RootGrid.Children.Add(_wizOverlay);
            WizRenderPage();
        }

        private void WizClose()
        {
            if (_wizOverlay != null) { RootGrid.Children.Remove(_wizOverlay); _wizOverlay = null; }
        }

        private void WizSetComplete(int idx, bool value = true) { _wizCompleted[idx] = value; WizRefreshNav(); }

        private void WizRefreshNav()
        {
            if (_wizDots == null || _wizBackBtn == null || _wizNextBtn == null) return;
            for (int i = 0; i < WizardPageCount; i++)
            {
                var dot = (TextBlock)_wizDots.Children[i];
                dot.Opacity = i == _wizPage ? 1.0 : _wizCompleted[i] ? 0.65 : 0.3;
            }
            _wizBackBtn.IsEnabled = _wizPage > 0;
            _wizNextBtn.Content = _wizPage == WizardPageCount - 1 ? TW("WIZARD_FINISH", "Finish") : TW("WIZARD_CONTINUE", "Continue");
            _wizNextBtn.IsEnabled = _wizCompleted[_wizPage];
        }

        private async void WizGoNext()
        {
            if (!_wizCompleted[_wizPage]) return;
            if (_wizPage == 3 && !_wizProfileCreated && !await WizCreateProfileAsync()) return;
            if (_wizPage == WizardPageCount - 1)
            {
                WizApplySettings();
                SetupFlags.MarkSetupDone();
                WizClose();
                LoadGameProfilesList();
                LoadGameProfiles();
                return;
            }
            _wizPage++;
            WizRenderPage();
        }

        private void WizRenderPage()
        {
            if (_wizBody == null) return;
            _wizBody.Content = _wizPage switch
            {
                0 => WizGreetPage(),
                1 => WizAccountPage(),
                2 => WizJavaPage(),
                3 => WizProfilePage(),
                _ => WizSettingsPage()
            };
            WizRefreshNav();
        }

        private StackPanel WizNewPage(string subtitleKey, string subtitleFallback, string headKey, string headFallback, string descKey, string descFallback)
        {
            if (_wizSubtitle != null) _wizSubtitle.Text = TW(subtitleKey, subtitleFallback);
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock
            {
                Text = TW(headKey, headFallback), FontSize = 19,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(0, 10, 0, 0)
            });
            panel.Children.Add(new TextBlock
            {
                Text = TW(descKey, descFallback), FontSize = 12, Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
            });
            return panel;
        }

        private UIElement WizGreetPage()
        {
            var panel = WizNewPage("WIZARD_STEP_GREET", "Welcome", "WIZARD_GREET_TITLE", "Welcome to OrangLauncher!",
                "WIZARD_GREET_TAGLINE", "Let's get you set up in a few quick steps.");
            (string, string)[] steps =
            {
                (TW("WIZARD_GREET_STEP_ACCOUNT", "Add your account"), TW("WIZARD_GREET_STEP_ACCOUNT_DESC", "Sign in with Microsoft or play offline.")),
                (TW("WIZARD_GREET_STEP_JAVA", "Pick a Java runtime"), TW("WIZARD_GREET_STEP_JAVA_DESC", "We recommend the newest installed Java.")),
                (TW("WIZARD_GREET_STEP_PROFILE", "Create your first profile"), TW("WIZARD_GREET_STEP_PROFILE_DESC", "Choose a Minecraft version, loader and RAM.")),
                (TW("WIZARD_GREET_STEP_SETTINGS", "Tune the launcher"), TW("WIZARD_GREET_STEP_SETTINGS_DESC", "Theme, Discord presence and cleanup options."))
            };
            for (int i = 0; i < steps.Length; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 7, 0, 7), ColumnSpacing = 14 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var num = new Border
                {
                    Width = 26, Height = 26, CornerRadius = new CornerRadius(13),
                    Background = Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush,
                    Child = new TextBlock
                    {
                        Text = (i + 1).ToString(), FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Grid.SetColumn(num, 0); row.Children.Add(num);
                var info = new StackPanel();
                info.Children.Add(new TextBlock { Text = steps[i].Item1, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                info.Children.Add(new TextBlock { Text = steps[i].Item2, FontSize = 11, Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(info, 1); row.Children.Add(info);
                panel.Children.Add(row);
            }
            var skip = new Button { Content = TW("WIZARD_SKIP_ALL", "Skip setup"), Margin = new Thickness(0, 16, 0, 0) };
            skip.Click += (s, e) => { SetupFlags.MarkSetupDone(); WizClose(); };
            panel.Children.Add(skip);
            return panel;
        }

        private UIElement WizAccountPage()
        {
            var panel = WizNewPage("WIZARD_STEP_ACCOUNT", "Step 2 of 5 - Account", "WIZARD_ACCOUNT_TITLE", "Add your account",
                "WIZARD_ACCOUNT_DESC", "Sign in with your Microsoft account to play online, or add an offline account.");
            var status = new TextBlock { Opacity = 0.75, Margin = new Thickness(0, 0, 0, 8) };
            void UpdateStatus()
            {
                var accounts = ProfileManager.Instance.GetProfiles();
                if (accounts.Count > 0)
                {
                    status.Text = string.Format(TW("WIZARD_ACCOUNT_SIGNED_IN", "Signed in: {0}"), string.Join(", ", accounts.Select(a => a.Username)));
                    status.Opacity = 1.0;
                    WizSetComplete(1);
                }
                else status.Text = TW("WIZARD_ACCOUNT_NO_ACCOUNTS", "No accounts yet.");
            }
            UpdateStatus();
            panel.Children.Add(status);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var ms = new Button { Content = TW("WIZARD_ACCOUNT_LOGIN_MS", "Sign in with Microsoft"), Style = Application.Current.Resources["AccentButtonStyle"] as Style };
            ms.Click += async (s, e) =>
            {
                var loginWindow = new MicrosoftLoginDialog();
                loginWindow.Activate();
                var confirmed = await loginWindow.WaitForResultAsync();
                if (confirmed && loginWindow.ResultProfile != null)
                {
                    ProfileManager.Instance.AddOrUpdateProfile(loginWindow.ResultProfile);
                    LoadAccountsData();
                    LoadProfiles();
                }
                UpdateStatus();
            };
            row.Children.Add(ms);
            var offline = new Button { Content = TW("WIZARD_ACCOUNT_OFFLINE", "Add offline account") };
            offline.Click += async (s, e) =>
            {
                var input = new TextBox { PlaceholderText = TW("WIZARD_ACCOUNT_OFFLINE_PROMPT", "Enter your offline username:") };
                var dialog = new ContentDialog
                {
                    Title = TW("WIZARD_ACCOUNT_OFFLINE", "Add offline account"),
                    Content = input,
                    PrimaryButtonText = TW("GAME_PROFILES_CREATE_BTN", "Create"),
                    CloseButtonText = TW("CANCEL", "Cancel"),
                    XamlRoot = Content.XamlRoot
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
                {
                    ProfileManager.Instance.AddProfile(input.Text.Trim(), "Offline");
                    LoadAccountsData();
                    LoadProfiles();
                }
                UpdateStatus();
            };
            row.Children.Add(offline);
            var skip = new Button { Content = TW("WIZARD_ACCOUNT_SKIP", "Skip for now") };
            skip.Click += (s, e) => { WizSetComplete(1); _wizPage++; WizRenderPage(); };
            row.Children.Add(skip);
            panel.Children.Add(row);
            return panel;
        }

        private UIElement WizJavaPage()
        {
            var panel = WizNewPage("WIZARD_STEP_JAVA", "Step 3 of 5 - Java", "WIZARD_JAVA_TITLE", "Pick a Java runtime",
                "WIZARD_JAVA_DESC", "Minecraft needs Java. \"Auto\" picks the right one per game version; missing runtimes are downloaded automatically.");
            _wizJavaCombo = new ComboBox { MinWidth = 320 };
            _wizJavaCombo.Items.Add(TW("WIZARD_JAVA_AUTO", "Auto (Detect / download when needed)"));
            foreach (var ver in JavaManager.GetAvailableVersions())
            {
                var label = JavaManager.IsJavaInstalled(ver) ? $"Java {ver} ({TW("WIZARD_JAVA_INSTALLED", "installed")})" : $"Java {ver}";
                _wizJavaCombo.Items.Add(label);
            }
            _wizJavaCombo.SelectedIndex = 0;
            panel.Children.Add(_wizJavaCombo);
            WizSetComplete(2);
            return panel;
        }

        private UIElement WizProfilePage()
        {
            var panel = WizNewPage("WIZARD_STEP_PROFILE", "Step 4 of 5 - First profile", "WIZARD_PROFILE_TITLE", "Create your first profile",
                "WIZARD_PROFILE_DESC", "You can add more profiles later from the Game Profiles tab.");
            var grid = new Grid { ColumnSpacing = 10, RowSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 4; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            void AddLabel(int row, string text)
            {
                var l = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(l, row); Grid.SetColumn(l, 0); grid.Children.Add(l);
            }
            void AddField(int row, FrameworkElement el)
            {
                Grid.SetRow(el, row); Grid.SetColumn(el, 1); grid.Children.Add(el);
            }

            AddLabel(0, TW("GAME_PROFILES_NAME", "Name"));
            _wizNameBox = new TextBox();
            var baseName = TW("WIZARD_PROFILE_DEFAULT_NAME", "My Profile");
            var name = baseName; int n = 1;
            while (InstanceManager.Instance.GetInstanceByName(name) != null) name = $"{baseName} {++n}";
            _wizNameBox.Text = name;
            _wizNameBox.TextChanged += (s, e) => WizSetComplete(3, !string.IsNullOrWhiteSpace(_wizNameBox.Text));
            AddField(0, _wizNameBox);

            AddLabel(1, TW("GAME_PROFILES_LOADER", "Mod Loader"));
            _wizLoaderCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var l in new[] { "vanilla", "forge", "neoforge", "fabric", "quilt" }) _wizLoaderCombo.Items.Add(l);
            _wizLoaderCombo.SelectedIndex = 0;
            AddField(1, _wizLoaderCombo);

            AddLabel(2, TW("GAME_PROFILES_VERSION", "Version"));
            _wizVersionCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            AddField(2, _wizVersionCombo);
            _ = WizLoadVersionsAsync();

            AddLabel(3, TW("GAME_PROFILES_RAM", "RAM"));
            var ramPanel = new Grid { ColumnSpacing = 10 };
            ramPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ramPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            long totalMb = SystemPerformanceHelper.GetTotalRamMB();
            _wizRamSlider = new Slider
            {
                Minimum = 1024,
                Maximum = Math.Max(2048, totalMb),
                Value = Math.Min(4096, Math.Max(2048, totalMb / 2)),
                StepFrequency = 512
            };
            var ramText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 70, Text = $"{(int)_wizRamSlider.Value} MB" };
            _wizRamSlider.ValueChanged += (s, e) => ramText.Text = $"{(int)_wizRamSlider.Value} MB";
            Grid.SetColumn(_wizRamSlider, 0); ramPanel.Children.Add(_wizRamSlider);
            Grid.SetColumn(ramText, 1); ramPanel.Children.Add(ramText);
            AddField(3, ramPanel);

            panel.Children.Add(grid);
            WizSetComplete(3, !string.IsNullOrWhiteSpace(_wizNameBox.Text));
            return panel;
        }

        private async Task WizLoadVersionsAsync()
        {
            try
            {
                var versions = await GameProfileManager.Instance.GetVersions();
                if (_wizVersionCombo == null) return;
                _wizVersionCombo.ItemsSource = versions.Select(v => v.Id).ToList();
                if (versions.Count > 0) _wizVersionCombo.SelectedIndex = 0;
            }
            catch { }
        }

        private async Task<bool> WizCreateProfileAsync()
        {
            var name = _wizNameBox?.Text?.Trim() ?? "";
            var version = _wizVersionCombo?.SelectedItem?.ToString() ?? "";
            var loader = _wizLoaderCombo?.SelectedItem?.ToString() ?? "vanilla";
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(version))
            {
                await ShowMessageAsync(TW("WIZARD_PROFILE_MISSING", "Please enter a name and pick a version."));
                return false;
            }
            if (InstanceManager.Instance.GetInstanceByName(name) != null)
            {
                await ShowMessageAsync(string.Format(TW("WIZARD_PROFILE_NAME_TAKEN", "A profile called '{0}' already exists."), name));
                return false;
            }
            // Same flow as the profile editor install the mod loader first, then store the installed version id
            var installedVersionName = version;
            if (loader != "vanilla")
            {
                try
                {
                    if (_wizNextBtn != null) { _wizNextBtn.IsEnabled = false; }
                    if (_wizSubtitle != null) _wizSubtitle.Text = string.Format(TW("WIZARD_PROFILE_INSTALLING", "Installing {0} for {1}..."), loader, version);
                    installedVersionName = await ModLoaderInstaller.InstallLoaderAsync(PlatformPaths.GetMinecraftDir(), version, loader, null);
                    LogMessage($"Wizard installed {loader}: {installedVersionName}");
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync($"Failed to install {loader}: {ex.Message}", "Error");
                    return false;
                }
                finally
                {
                    if (_wizNextBtn != null) _wizNextBtn.IsEnabled = true;
                    WizRefreshNav();
                }
            }
            var ram = ((int)(_wizRamSlider?.Value ?? 4096)).ToString();
            var instance = InstanceManager.Instance.CreateInstance(name, installedVersionName, loader, ram);
            if (instance != null)
            {
                instance.Version = installedVersionName;
                instance.InstalledVersionName = installedVersionName;
                InstanceManager.Instance.SaveInstances();
            }
            _wizProfileCreated = instance != null;
            return _wizProfileCreated;
        }

        private UIElement WizSettingsPage()
        {
            var panel = WizNewPage("WIZARD_STEP_SETTINGS", "Step 5 of 5 - Settings", "WIZARD_SETTINGS_TITLE", "Recommended settings",
                "WIZARD_SETTINGS_DESC", "You can change all of these later in Settings.");
            _wizDiscordCheck = new CheckBox { Content = TW("WIZARD_SETTINGS_DISCORD", "Enable Discord Rich Presence"), IsChecked = true };
            _wizTelemetryCheck = new CheckBox { Content = TW("WIZARD_SETTINGS_TELEMETRY", "Delete Minecraft telemetry on startup"), IsChecked = true };
            panel.Children.Add(_wizDiscordCheck);
            panel.Children.Add(_wizTelemetryCheck);
            var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 10, 0, 0) };
            themeRow.Children.Add(new TextBlock { Text = TW("WIZARD_SETTINGS_THEME", "Theme"), VerticalAlignment = VerticalAlignment.Center });
            _wizThemeCombo = new ComboBox { MinWidth = 200 };
            _wizThemeCombo.Items.Add(new ComboBoxItem { Content = "Dark Mode", Tag = "dark" });
            _wizThemeCombo.Items.Add(new ComboBoxItem { Content = "Light Mode", Tag = "light" });
            _wizThemeCombo.Items.Add(new ComboBoxItem { Content = "System Default", Tag = "system" });
            _wizThemeCombo.SelectedIndex = 0;
            themeRow.Children.Add(_wizThemeCombo);
            panel.Children.Add(themeRow);
            panel.Children.Add(new TextBlock
            {
                Text = TW("WIZARD_SETTINGS_FINISH_HINT", "Press Finish and you're ready to play!"),
                FontSize = 11, Opacity = 0.7, Margin = new Thickness(0, 12, 0, 0)
            });
            WizSetComplete(4);
            return panel;
        }

        private void WizApplySettings()
        {
            try
            {
                DiscordRpcCheckBox.IsChecked = _wizDiscordCheck?.IsChecked ?? true;
                DeleteTelemetryCheckBox.IsChecked = _wizTelemetryCheck?.IsChecked ?? false;
                if (_wizThemeCombo?.SelectedItem is ComboBoxItem item && item.Tag is string themeKey)
                {
                    foreach (ComboBoxItem t in ThemeComboBox.Items)
                        if (t.Tag?.ToString() == themeKey) { ThemeComboBox.SelectedItem = t; break; }
                    ApplyTheme(themeKey);
                }
                SaveSettings();
            }
            catch { }
        }
    }
}
