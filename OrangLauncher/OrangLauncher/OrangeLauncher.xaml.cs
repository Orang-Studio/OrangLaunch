using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using OrangLauncher.Backend;
using OrangLauncher.Managers;
using OrangLauncher.Models;
using OrangLauncher.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;
namespace OrangLauncher
{
    public class InstanceCardModel
    {
        public string InstanceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string ModLoader { get; set; } = "";
        public BitmapImage? IconSource { get; set; }
    }
    public class UserProfileDisplay
    {
        public string Username { get; set; } = "";
        public string TypeDisplay { get; set; } = "";
        public UserProfile Profile { get; set; } = null!;
    }
    public class JavaListItem
    {
        public int MajorVersion { get; set; }
        public string DisplayName { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string InstallButtonText { get; set; } = "Install";
        public bool IsInstalled { get; set; }
        public bool CanInstall { get; set; } = true;
    }
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
        private string _statusText = "Welcome";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); UpdateStatusLabel(); }
        }
        private string _gameProfileInfo = "No profile selected";
        public string GameProfileInfo
        {
            get => _gameProfileInfo;
            set { _gameProfileInfo = value; OnPropertyChanged(); UpdateProfileInfoLabel(); }
        }
        private readonly ObservableCollection<InstanceCardModel> _gameInstanceCards = [];
        public ObservableCollection<InstanceCardModel> GameInstances => _gameInstanceCards;
        private readonly ObservableCollection<UserProfile> _userProfiles = [];
        private readonly ObservableCollection<PluginInfo> _plugins = [];
        private List<ModInfo> _allMods = [];
        private List<ResourcePackInfo> _allResourcePacks = [];
        private List<ResourcePackInfo> _allShaderPacks = [];
        private DiscordRpcManager? _discordRpcManager;
        private bool _isEditingInstance = false;
        private string? _editingInstanceId;
        private string? _tempIconPath;
        private Process? _currentProcess;
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        private void UpdateStatusLabel()
        {
            DispatcherQueue.TryEnqueue(() => { if (StatusLabel != null) StatusLabel.Text = _statusText; });
        }
        private void UpdateProfileInfoLabel()
        {
            DispatcherQueue.TryEnqueue(() => { if (GameProfileInfoLabel != null) GameProfileInfoLabel.Text = _gameProfileInfo; });
        }
        public MainWindow()
        {
            this.InitializeComponent();
            var appWindow = GetAppWindow();
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 720));
                appWindow.Title = "OrangLauncher";
                appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(40, 128, 128, 128);
                appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(80, 128, 128, 128);
            }
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "images", "orange.ico");
                if (File.Exists(iconPath))
                {
                    appWindow?.SetIcon(iconPath);
                }
            }
            catch { }
            ApplyTheme("dark");
            this.SetTitleBar(TitleBarDragArea);
            RootGrid.Loaded += RootGrid_Loaded;
        }
        private AppWindow? GetAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }
        private IntPtr GetHwnd() => WindowNative.GetWindowHandle(this);
        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainNavView.MenuItems.Count > 0)
                    MainNavView.SelectedItem = MainNavView.MenuItems[0];
                InitializeApplication();
            }
            catch (Exception ex)
            {
                LogMessage($"Error during window load: {ex.Message}");
            }
        }
        private async void InitializeApplication()
        {
            try
            {
                LogMessage("Initializing application...");
                LocalizationManager.LoadLanguage("en-US");
                LoadProfiles();
                ProfileComboBox.SelectionChanged += ProfileComboBox_SelectionChanged;
                GameProfileComboBox.SelectionChanged += GameProfileComboBox_SelectionChanged;
                LoadGameProfiles();
                LoadSettings();
                LoadGameProfilesList();
                InitializeSettings();
                ApplyLocalization();
                LoadResourcePacks();
                LoadShaderPacks();
                LoadMods();
                LoadServerList();
                try
                {
                    await NewsWebView.EnsureCoreWebView2Async();
                    LogMessage("WebView2 initialized successfully");
                }
                catch (Exception ex)
                {
                    LogMessage($"WebView2 initialization failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during initialization: {ex.Message}");
            }
        }
        private void ApplyLocalization()
        {
            try
            {
                if (MainNavView.MenuItems.Count > 0 && MainNavView.MenuItems[0] is NavigationViewItem nav0)
                    nav0.Content = LocalizationManager.GetString("UPDATE_NOTES", "Update Notes");
                if (MainNavView.MenuItems.Count > 1 && MainNavView.MenuItems[1] is NavigationViewItem nav1)
                    nav1.Content = LocalizationManager.GetString("LAUNCHER_LOG", "Launcher Log");
                if (MainNavView.MenuItems.Count > 2 && MainNavView.MenuItems[2] is NavigationViewItem nav2)
                    nav2.Content = LocalizationManager.GetString("GAME_PROFILES", "Game Profiles");
                if (MainNavView.MenuItems.Count > 3 && MainNavView.MenuItems[3] is NavigationViewItem nav3)
                    nav3.Content = LocalizationManager.GetString("RESOURCE_PACKS", "Resource & Shader Packs");
                if (MainNavView.MenuItems.Count > 4 && MainNavView.MenuItems[4] is NavigationViewItem nav4)
                    nav4.Content = LocalizationManager.GetString("SERVER_MANAGER", "Server Manager");
                if (MainNavView.MenuItems.Count > 5 && MainNavView.MenuItems[5] is NavigationViewItem nav5)
                    nav5.Content = LocalizationManager.GetString("SETTINGS", "Settings");
                if (ResourcesNavView.MenuItems.Count > 0 && ResourcesNavView.MenuItems[0] is NavigationViewItem res0)
                    res0.Content = LocalizationManager.GetString("RES_SH_RP_TITLE", "Resource Packs");
                if (ResourcesNavView.MenuItems.Count > 1 && ResourcesNavView.MenuItems[1] is NavigationViewItem res1)
                    res1.Content = LocalizationManager.GetString("RES_SH_SP_TITLE", "Shader Packs");
                if (ResourcesNavView.MenuItems.Count > 2 && ResourcesNavView.MenuItems[2] is NavigationViewItem res2)
                    res2.Content = LocalizationManager.GetString("MODS_TAB_TITLE", "Modding");
                GameProfilesTitleText.Text = LocalizationManager.GetString("GAME_PROFILES_TITLE", "GAME PROFILES");
                NewProfileBtnText.Text = LocalizationManager.GetString("NEW_PROFILE", "NEW PROFILE");
                EditLblName.Text = LocalizationManager.GetString("GAME_PROFILES_NAME", "Name");
                EditLblModLoader.Text = LocalizationManager.GetString("GAME_PROFILES_LOADER", "Mod Loader");
                EditLblLoaderVersion.Text = LocalizationManager.GetString("GAME_PROFILES_LOADER_VERSION", "Loader Version");
                EditLblVersion.Text = LocalizationManager.GetString("GAME_PROFILES_VERSION", "Version");
                EditLblRam.Text = LocalizationManager.GetString("GAME_PROFILES_RAM", "RAM");
                EditLblPerformance.Text = LocalizationManager.GetString("PERFORMANCE", "Performance Tier");
                EditLblStartupArgs.Text = LocalizationManager.GetString("STARTUP_ARGS", "Startup Arguments");
                EditLblStartupArgsDesc.Text = LocalizationManager.GetString("PROFILE_JAVA_ARGS_DESC", "Shows all arguments that will be used. Fully editable.");
                EditLblJavaVersion.Text = LocalizationManager.GetString("JAVA_VERSION", "Java Version");
                SaveProfileBtnText.Text = LocalizationManager.GetString("GAME_PROFILES_SAVE_BTN", "Save");
                CancelProfileBtnText.Text = LocalizationManager.GetString("CANCEL", "Cancel");
                ServerManagerTitle.Text = LocalizationManager.GetString("SERVER_MANAGER", "Server Manager");
                AddServerBtnText.Text = LocalizationManager.GetString("ADD_SERVER", "Add Server");
                GeneralSettingsButton.Content = LocalizationManager.GetString("GENERAL", "General");
                AccountsSettingsButton.Content = LocalizationManager.GetString("ACCOUNTS", "Accounts");
                AdvancedSettingsButton.Content = LocalizationManager.GetString("ADVANCED", "Advanced");
                AboutSettingsButton.Content = LocalizationManager.GetString("ABOUT", "About");
                SettingsTitleText.Text = LocalizationManager.GetString("SETTINGS", "Settings");
                SettingsSubtitleText.Text = LocalizationManager.GetString("SETTINGS_SUBTITLE", "Configure your launcher preferences");
                SettingsGeneralTitle.Text = LocalizationManager.GetString("SETTINGS_GENERAL_TITLE", "General Settings");
                SettingsLanguageLabel.Text = LocalizationManager.GetString("LANGUAGE", "Language");
                SettingsLanguageDesc.Text = LocalizationManager.GetString("SETTINGS_LANGUAGE_DESC", "Choose your preferred language for the launcher interface.");
                SettingsLanguageNote.Text = LocalizationManager.GetString("SETTINGS_LANGUAGE_NOTE", "Note: Changing language requires restarting the application.");
                SettingsThemeLabel.Text = LocalizationManager.GetString("SETTINGS_CARD_THEME", "Theme");
                SettingsThemeDesc.Text = LocalizationManager.GetString("SETTINGS_THEME_DESC", "Choose your preferred visual theme.");
                SettingsThemeNote.Text = LocalizationManager.GetString("SETTINGS_THEME_NOTE", "Theme changes apply immediately.");
                SettingsAccountsTitle.Text = LocalizationManager.GetString("SETTINGS_ACCOUNTS_TITLE", "Account Management");
                SettingsMcAccountsLabel.Text = LocalizationManager.GetString("SETTINGS_MC_ACCOUNTS", "Minecraft Accounts");
                SettingsMcAccountsDesc.Text = LocalizationManager.GetString("SETTINGS_MC_ACCOUNTS_DESC", "Manage your Minecraft accounts for launching games.");
                AddMsAccountBtnText.Text = LocalizationManager.GetString("SETTINGS_ACCOUNT_ADD_MS", "Add Microsoft Account");
                AddOfflineAccountBtnText.Text = LocalizationManager.GetString("SETTINGS_ACCOUNT_ADD_OFFLINE", "Add Offline Account");
                SettingsSkinPreviewLabel.Text = LocalizationManager.GetString("SETTINGS_SKIN_PREVIEW", "Skin Preview");
                SkinInfoText.Text = LocalizationManager.GetString("SETTINGS_SELECT_ACCOUNT_VIEW", "Select an account to view skin");
                UploadSkinBtnText.Text = LocalizationManager.GetString("SETTINGS_UPLOAD_SKIN", "Upload Skin");
                ResetSkinBtnText.Text = LocalizationManager.GetString("SETTINGS_RESET_SKIN", "Reset Skin");
                SkinModelLabel.Text = LocalizationManager.GetString("SETTINGS_MODEL", "Model:");
                SkinModelSteve.Content = LocalizationManager.GetString("SETTINGS_STEVE", "Steve (Classic)");
                SkinModelAlex.Content = LocalizationManager.GetString("SETTINGS_ALEX", "Alex (Slim)");
                SettingsAdvancedTitle.Text = LocalizationManager.GetString("SETTINGS_ADVANCED_TITLE", "Advanced Settings");
                SettingsDiscordTitle.Text = LocalizationManager.GetString("SETTINGS_DISCORD_TITLE", "Discord Rich Presence");
                DiscordRpcCheckBox.Content = LocalizationManager.GetString("SETTINGS_DISCORD_ENABLE", "Enable Discord Rich Presence");
                SettingsDiscordDesc.Text = LocalizationManager.GetString("SETTINGS_DISCORD_DESC", "Show your Minecraft activity in Discord.");
                SettingsTelemetryTitle.Text = LocalizationManager.GetString("SETTINGS_TELEMETRY_TITLE", "Telemetry");
                DeleteTelemetryCheckBox.Content = LocalizationManager.GetString("SETTINGS_TELEMETRY_ENABLE", "Delete telemetry files on startup");
                SettingsTelemetryDesc.Text = LocalizationManager.GetString("SETTINGS_TELEMETRY_DESC", "Automatically clean up Minecraft telemetry data on launcher startup.");
                SettingsPluginsTitle.Text = LocalizationManager.GetString("SETTINGS_PLUGINS_TITLE", "Plugins");
                SettingsPluginsDesc.Text = LocalizationManager.GetString("SETTINGS_PLUGINS_DESC", "Manage launcher plugins and extensions.");
                AddPluginBtnText.Text = LocalizationManager.GetString("SETTINGS_ADD_PLUGIN", "Add Plugin");
                SettingsDebugTitle.Text = LocalizationManager.GetString("SETTINGS_DEBUG_TITLE", "Debug Mode");
                DebugModeCheckBox.Content = LocalizationManager.GetString("SETTINGS_DEBUG_ENABLE", "Enable debug mode");
                SettingsDebugDesc.Text = LocalizationManager.GetString("SETTINGS_DEBUG_DESC", "Show detailed logging and debugging information.");
                SettingsGpuTitle.Text = LocalizationManager.GetString("SETTINGS_GPU_TITLE", "GPU Settings");
                UseDiscreteGpuCheckBox.Content = LocalizationManager.GetString("SETTINGS_GPU_ENABLE", "Force use discrete GPU (NVIDIA/AMD)");
                SettingsGpuDesc.Text = LocalizationManager.GetString("SETTINGS_GPU_DESC", "Forces Minecraft to use your dedicated graphics card instead of integrated graphics.");
                JavaManagementTitle.Text = LocalizationManager.GetString("JAVA_MANAGEMENT", "Java Management");
                JavaManagementDesc.Text = LocalizationManager.GetString("JAVA_MANAGEMENT_DESC", "Install and manage Java runtimes used by the launcher.");
                AboutVersionText.Text = LocalizationManager.GetString("SETTINGS_ABOUT_VERSION", "Version: 5.0.0");
                AboutDescText.Text = LocalizationManager.GetString("SETTINGS_ABOUT_DESC", "A modern Minecraft launcher with advanced features.");
                AboutDevsText.Text = LocalizationManager.GetString("SETTINGS_ABOUT_DEVS", "Developed by: adasjusk and previously vakarux");
                CheckUpdatesBtnText.Text = LocalizationManager.GetString("SETTINGS_ABOUT_CHECK_UPDATES", "Check for Updates");
                ViewGitHubBtnText.Text = LocalizationManager.GetString("SETTINGS_ABOUT_GITHUB", "View on GitHub");
                FooterProfileLabel.Text = LocalizationManager.GetString("PROFILE", "PROFILE");
                FooterGameProfilesLabel.Text = LocalizationManager.GetString("GAME_PROFILES_TITLE", "GAME PROFILES");
                PlayButton.Content = LocalizationManager.GetString("PLAY", "PLAY");
                ManageAccountsButton.Content = LocalizationManager.GetString("MANAGE_ACCOUNTS", "Manage Accounts");
                ClearLogText.Text = LocalizationManager.GetString("CLEAR_LOG", "Clear Log");
                RpSearchLabel.Text = LocalizationManager.GetString("SEARCH", "Search:");
                RpRefreshBtn.Text = LocalizationManager.GetString("REFRESH", "Refresh");
                RpAddBtn.Text = LocalizationManager.GetString("ADD_PACK", "Add Pack");
                RpUpdateBtn.Text = LocalizationManager.GetString("UPDATE", "Update");
                RpDownloadBtn.Text = LocalizationManager.GetString("DOWNLOAD", "Download");
                RpRemoveBtn.Text = LocalizationManager.GetString("REMOVE", "Remove");
                RpOpenFolderBtn.Text = LocalizationManager.GetString("OPEN_FOLDER", "Open Folder");
                SpSearchLabel.Text = LocalizationManager.GetString("SEARCH", "Search:");
                SpAddBtn.Text = LocalizationManager.GetString("ADD_SHADER", "Add Shader");
                SpUpdateBtn.Text = LocalizationManager.GetString("UPDATE", "Update");
                SpDownloadBtn.Text = LocalizationManager.GetString("DOWNLOAD", "Download");
                SpRefreshBtn.Text = LocalizationManager.GetString("REFRESH", "Refresh");
                SpRemoveBtn.Text = LocalizationManager.GetString("REMOVE", "Remove");
                SpOpenFolderBtn.Text = LocalizationManager.GetString("OPEN_FOLDER", "Open Folder");
                ModsTitleText.Text = LocalizationManager.GetString("MODS", "Mods");
                ModsSearchLabel.Text = LocalizationManager.GetString("SEARCH", "Search:");
                ModAddBtn.Text = LocalizationManager.GetString("ADD_MOD", "Add Mod");
                ModUpdateBtn.Text = LocalizationManager.GetString("UPDATE", "Update");
                ModDownloadBtn.Text = LocalizationManager.GetString("DOWNLOAD", "Download");
                ModRefreshBtn.Text = LocalizationManager.GetString("REFRESH", "Refresh");
                ModRemoveBtn.Text = LocalizationManager.GetString("REMOVE_SELECTED", "Remove Selected");
                ModOpenFolderBtn.Text = LocalizationManager.GetString("OPEN_FOLDER", "Open Folder");
                ModImportBtn.Text = LocalizationManager.GetString("IMPORT_MRPACK", "Import .mrpack");
                SrvRefreshBtn.Text = LocalizationManager.GetString("REFRESH", "Refresh");
                SrvRemoveBtn.Text = LocalizationManager.GetString("REMOVE_SELECTED", "Remove Selected");
                RefreshPluginsBtnText.Text = LocalizationManager.GetString("REFRESH", "Refresh");
                RefreshJavaBtnText.Text = LocalizationManager.GetString("JAVA_REFRESH", "Refresh");
            }
            catch (Exception ex)
            {
                LogMessage($"Error applying localization: {ex.Message}");
            }
        }
        private void ApplyTheme(string themeKey)
        {
            if (RootGrid == null) return;
            RootGrid.RequestedTheme = themeKey switch
            {
                "dark" => ElementTheme.Dark,
                "light" => ElementTheme.Light,
                "system" => ElementTheme.Default,
                _ => ElementTheme.Dark
            };
            UpdateTitleBarButtonColors(themeKey);
        }
        private void UpdateTitleBarButtonColors(string themeKey)
        {
            var appWindow = GetAppWindow();
            if (appWindow?.TitleBar == null) return;
            bool isDark = themeKey == "dark" ||
                          (themeKey == "system" && Application.Current.RequestedTheme == ApplicationTheme.Dark);
            appWindow.TitleBar.ButtonForegroundColor = isDark
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black;
            appWindow.TitleBar.ButtonInactiveForegroundColor = isDark
                ? Windows.UI.Color.FromArgb(120, 255, 255, 255)
                : Windows.UI.Color.FromArgb(120, 0, 0, 0);
            appWindow.TitleBar.ButtonHoverForegroundColor = isDark
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black;
            appWindow.TitleBar.ButtonPressedForegroundColor = isDark
                ? Microsoft.UI.Colors.White
                : Microsoft.UI.Colors.Black;
        }
        private void InitializeDiscordRpc()
        {
            try
            {
                if (DiscordRpcCheckBox.IsChecked == true)
                {
                    _discordRpcManager ??= new DiscordRpcManager();
                    _discordRpcManager.Initialize();
                    _discordRpcManager.UpdatePresence("Idling in launcher");
                }
                else
                {
                    _discordRpcManager?.Dispose();
                    _discordRpcManager = null;
                }
            }
            catch (Exception ex) { LogMessage($"Discord RPC error: {ex.Message}"); }
        }
        private void UpdateDiscordRpc(string state, string details = "")
        {
            try
            {
                if (DiscordRpcCheckBox.IsChecked == true && _discordRpcManager != null)
                    _discordRpcManager.UpdatePresence(state, details);
            }
            catch { }
        }
        private void MainNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                NewsPanel.Visibility = tag == "news" ? Visibility.Visible : Visibility.Collapsed;
                LogPanel.Visibility = tag == "log" ? Visibility.Visible : Visibility.Collapsed;
                ProfilesPanel.Visibility = tag == "profiles" ? Visibility.Visible : Visibility.Collapsed;
                ResourcesPanel.Visibility = tag == "resources" ? Visibility.Visible : Visibility.Collapsed;
                ServersPanel.Visibility = tag == "servers" ? Visibility.Visible : Visibility.Collapsed;
                SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;
                if (tag == "resources") { LoadResourcePacks(); LoadShaderPacks(); LoadMods(); }
                else if (tag == "servers") LoadServerList();
            }
        }
        private void ResourcesNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                ResourcePacksSubPanel.Visibility = tag == "resourcepacks" ? Visibility.Visible : Visibility.Collapsed;
                ShaderPacksSubPanel.Visibility = tag == "shaderpacks" ? Visibility.Visible : Visibility.Collapsed;
                ModdingSubPanel.Visibility = tag == "modding" ? Visibility.Visible : Visibility.Collapsed;
                if (tag == "resourcepacks") LoadResourcePacks();
                else if (tag == "shaderpacks") LoadShaderPacks();
                else if (tag == "modding") LoadMods();
            }
        }
        private void GetCurrentProfileInfo(out string name, out bool isInstance)
        {
            name = "";
            isInstance = false;
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance) { name = instance.Name; isInstance = true; }
                else if (item.Tag is GameProfile profile) { name = profile.Name; isInstance = false; }
            }
        }
        private string GetCurrentModsPath()
        {
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance) return instance.ModsDir;
                else if (item.Tag is GameProfile profile) return GameProfileManager.Instance.GetModsDirectory(profile.Id);
            }
            return Path.Combine(PlatformPaths.GetMinecraftDir(), "mods");
        }
        private string GetCurrentResourcePacksPath(bool isShader)
        {
            string subDir = isShader ? "shaderpacks" : "resourcepacks";
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance) return isShader ? instance.ShaderPacksDir : instance.ResourcePacksDir;
                else if (item.Tag is GameProfile profile) return Path.Combine(profile.GameDir ?? PlatformPaths.GetMinecraftDir(), subDir);
            }
            return Path.Combine(PlatformPaths.GetMinecraftDir(), subDir);
        }
        private void LoadMods()
        {
            try
            {
                string path = GetCurrentModsPath();
                var mods = ModManager.Instance.GetMods(path);
                _allMods = [.. mods];
                ApplyModFilter();
                ModCountTextBlock.Text = $"{mods.Count} mods loaded";
                GetCurrentProfileInfo(out string name, out bool isInstance);
                ModsProfileInfoTextBlock.Text = isInstance ? $" | Instance: {name}" : $" | Profile: {name}";
            }
            catch (Exception ex) { LogMessage($"Error loading mods: {ex.Message}"); }
        }
        private void LoadResourcePacks()
        {
            try
            {
                string path = GetCurrentResourcePacksPath(false);
                var packs = ResourceManager.Instance.GetPacks(path, false);
                _allResourcePacks = [.. packs];
                ApplyResourcePackFilter();
            }
            catch (Exception ex) { LogMessage($"Error loading resource packs: {ex.Message}"); }
        }
        private void LoadShaderPacks()
        {
            try
            {
                string path = GetCurrentResourcePacksPath(true);
                var packs = ResourceManager.Instance.GetPacks(path, true);
                _allShaderPacks = [.. packs];
                ApplyShaderPackFilter();
            }
            catch (Exception ex) { LogMessage($"Error loading shader packs: {ex.Message}"); }
        }
        private void ApplyModFilter()
        {
            var filter = ModsSearchTextBox.Text;
            ModsListBox.ItemsSource = string.IsNullOrWhiteSpace(filter)
                ? new ObservableCollection<ModInfo>(_allMods)
                : new ObservableCollection<ModInfo>(_allMods.Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ApplyResourcePackFilter()
        {
            var filter = ResourcePacksSearchTextBox.Text;
            ResourcePacksListBox.ItemsSource = string.IsNullOrWhiteSpace(filter)
                ? new ObservableCollection<ResourcePackInfo>(_allResourcePacks)
                : new ObservableCollection<ResourcePackInfo>(_allResourcePacks.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ApplyShaderPackFilter()
        {
            var filter = ShaderPacksSearchTextBox.Text;
            ShaderPacksListBox.ItemsSource = string.IsNullOrWhiteSpace(filter)
                ? new ObservableCollection<ResourcePackInfo>(_allShaderPacks)
                : new ObservableCollection<ResourcePackInfo>(_allShaderPacks.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ModsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyModFilter();
        private void ResourcePacksSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyResourcePackFilter();
        private void ShaderPacksSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyShaderPackFilter();
        private void AddModButton_Click(object sender, RoutedEventArgs e) { ModManager.Instance.AddMods(GetCurrentModsPath()); LoadMods(); }
        private void RemoveModButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModsListBox.SelectedItems.Cast<ModInfo>().ToList();
            if (selectedMods.Count > 0) { ModManager.Instance.RemoveMods(selectedMods); LoadMods(); }
        }
        private void OpenModsFolderButton_Click(object sender, RoutedEventArgs e) => ModManager.Instance.OpenModsFolder(GetCurrentModsPath());
        private void RefreshModsButton_Click(object sender, RoutedEventArgs e) => LoadMods();
        private void AddResourcePackButton_Click(object sender, RoutedEventArgs e) { ResourceManager.Instance.AddPacks(GetCurrentResourcePacksPath(false), false); LoadResourcePacks(); }
        private void RefreshResourcePacksButton_Click(object sender, RoutedEventArgs e) => LoadResourcePacks();
        private void RemoveResourcePackButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ResourcePacksListBox.SelectedItems.Cast<ResourcePackInfo>().ToList();
            if (selected.Count > 0) { ResourceManager.Instance.RemovePacks(selected); LoadResourcePacks(); }
        }
        private void OpenResourcePacksButton_Click(object sender, RoutedEventArgs e) => ResourceManager.Instance.OpenFolder(GetCurrentResourcePacksPath(false));
        private void AddShaderPackButton_Click(object sender, RoutedEventArgs e) { ResourceManager.Instance.AddPacks(GetCurrentResourcePacksPath(true), true); LoadShaderPacks(); }
        private void RefreshShaderPacksButton_Click(object sender, RoutedEventArgs e) => LoadShaderPacks();
        private void RemoveShaderPackButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ShaderPacksListBox.SelectedItems.Cast<ResourcePackInfo>().ToList();
            if (selected.Count > 0) { ResourceManager.Instance.RemovePacks(selected); LoadShaderPacks(); }
        }
        private void OpenShaderPacksButton_Click(object sender, RoutedEventArgs e) => ResourceManager.Instance.OpenFolder(GetCurrentResourcePacksPath(true));
        private void GetCurrentInstanceVersionInfo(out string version, out string loader)
        {
            version = "1.20.1"; loader = "vanilla";
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance) { version = instance.Version; loader = instance.ModLoader; }
                else if (item.Tag is GameProfile profile) { version = profile.Version; loader = profile.ModLoader; }
            }
            loader = loader.ToLowerInvariant();
        }
        private void UpdateResourcePacksButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out _);
            new UpdateWindow(GetCurrentResourcePacksPath(false), "resourcepack", version).Activate();
            LoadResourcePacks();
        }
        private void DownloadResourcePacksButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out _);
            new DownloadWindow(GetCurrentResourcePacksPath(false), "resourcepack", version).Activate();
            LoadResourcePacks();
        }
        private void UpdateShaderPacksButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out _);
            new UpdateWindow(GetCurrentResourcePacksPath(true), "shader", version).Activate();
            LoadShaderPacks();
        }
        private void DownloadShaderPacksButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out _);
            new DownloadWindow(GetCurrentResourcePacksPath(true), "shader", version).Activate();
            LoadShaderPacks();
        }
        private async void UpdateModsButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out string loader);
            if (loader is "vanilla" or "none")
            {
                await ShowMessageAsync("Please select a mod loader (Fabric/Forge) in your profile settings before updating mods.", "No Loader Selected");
                return;
            }
            new UpdateWindow(GetCurrentModsPath(), "mod", version, loader).Activate();
            LoadMods();
        }
        private async void DownloadModsButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out string loader);
            if (loader is "vanilla" or "none")
            {
                await ShowMessageAsync("Please select a mod loader (Fabric/Forge) in your profile settings before downloading mods.", "No Loader Selected");
                return;
            }
            new DownloadWindow(GetCurrentModsPath(), "mod", version, loader).Activate();
            LoadMods();
        }
        private void LoadServerList()
        {
            try
            {
                ServersListBox.ItemsSource = null;
                if (GameProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is MinecraftInstance instance)
                {
                    var path = instance.MinecraftDir;
                    if (Directory.Exists(path))
                    {
                        var servers = ServerListManager.Instance.LoadServers(path);
                        var viewModels = new ObservableCollection<ServerViewModel>();
                        bool supportsQuickPlay = SupportsQuickPlay(instance.Version);
                        foreach (var server in servers)
                            viewModels.Add(new ServerViewModel(server) { SupportsQuickPlay = supportsQuickPlay });
                        ServersListBox.ItemsSource = viewModels;
                    }
                }
            }
            catch (Exception ex) { LogMessage($"Error loading servers: {ex.Message}"); }
        }
        private bool SupportsQuickPlay(string version)
        {
            try
            {
                var parts = version.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                {
                    if (major > 1) return true;
                    if (major == 1 && minor > 20) return true;
                    if (major == 1 && minor == 20 && parts.Length >= 3 && int.TryParse(parts[2], out int patch))
                        return patch >= 3;
                }
                return false;
            }
            catch { return false; }
        }
        private void RefreshServerButton_Click(object sender, RoutedEventArgs e) => LoadServerList();
        private async void AddServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GameProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is MinecraftInstance instance)
                {
                    var nameDialog = new ContentDialog
                    {
                        Title = "Add Server",
                        Content = new TextBox { PlaceholderText = "Server Name", Name = "ServerNameBox" },
                        PrimaryButtonText = "Next",
                        CloseButtonText = "Cancel",
                        XamlRoot = Content.XamlRoot
                    };
                    var nameResult = await nameDialog.ShowAsync();
                    if (nameResult != ContentDialogResult.Primary) return;
                    var name = ((TextBox)nameDialog.Content).Text;
                    if (string.IsNullOrWhiteSpace(name)) return;
                    var ipDialog = new ContentDialog
                    {
                        Title = "Server Address",
                        Content = new TextBox { PlaceholderText = "mc.hypixel.net" },
                        PrimaryButtonText = "Add",
                        CloseButtonText = "Cancel",
                        XamlRoot = Content.XamlRoot
                    };
                    var ipResult = await ipDialog.ShowAsync();
                    if (ipResult != ContentDialogResult.Primary) return;
                    var ip = ((TextBox)ipDialog.Content).Text;
                    if (string.IsNullOrWhiteSpace(ip)) return;
                    var path = instance.MinecraftDir;
                    Directory.CreateDirectory(path);
                    var servers = ServerListManager.Instance.LoadServers(path);
                    servers.Add(new ServerInfo { Name = name, Ip = ip });
                    ServerListManager.Instance.SaveServers(path, servers);
                    LoadServerList();
                    await ShowMessageAsync($"Server '{name}' added successfully!", "Server Added");
                }
                else
                {
                    await ShowMessageAsync("Please select a Game Instance first.");
                }
            }
            catch (Exception ex) { await ShowMessageAsync($"Error adding server: {ex.Message}"); }
        }
        private void RemoveServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServersListBox.SelectedItem is ServerViewModel viewModel && GameProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is MinecraftInstance instance)
            {
                var server = viewModel.Server;
                var path = instance.MinecraftDir;
                var servers = ServerListManager.Instance.LoadServers(path);
                var toRemove = servers.FirstOrDefault(s => s.Name == server.Name && s.Ip == server.Ip);
                if (toRemove != null) { servers.Remove(toRemove); ServerListManager.Instance.SaveServers(path, servers); LoadServerList(); }
            }
        }
        private void ManageAccountsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var navItem in MainNavView.MenuItems.Cast<NavigationViewItem>())
            {
                if (navItem.Tag?.ToString() == "settings") { MainNavView.SelectedItem = navItem; break; }
            }
            ShowSettingsPanel("Accounts");
        }
        private void SelectAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserProfile profile)
            {
                ProfileManager.Instance.SetSelectedProfile(profile.Uuid);
                LoadProfiles();
                LoadAccountsData();
                StatusText = $"Switched to account: {profile.Username}";
            }
        }
        private async void RemoveAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserProfile profile)
            {
                if (await ShowConfirmAsync($"Remove account '{profile.Username}'?", "Remove Account"))
                {
                    ProfileManager.Instance.RemoveProfile(profile.Uuid);
                    LoadAccountsData();
                    LoadProfiles();
                }
            }
        }
        private void SelectInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string instanceId)
            {
                InstanceManager.Instance.SetSelectedInstance(instanceId);
                LoadGameProfilesList();
                LoadGameProfiles();
            }
        }
        private void EditInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string instanceId)
            {
                var instance = InstanceManager.Instance.GetInstance(instanceId);
                if (instance == null) return;
                _isEditingInstance = true;
                _editingInstanceId = instance.InstanceId;
                _tempIconPath = instance.Icon;
                ProfileNameTextBox.Text = instance.Name;
                foreach (ComboBoxItem cbItem in ProfileModLoaderComboBox.Items)
                {
                    if (cbItem.Content?.ToString() is string s && string.Equals(s, instance.ModLoader, StringComparison.OrdinalIgnoreCase))
                    { ProfileModLoaderComboBox.SelectedItem = cbItem; break; }
                }
                ProfileVersionComboBox.SelectedItem = instance.Version;
                if (!string.IsNullOrEmpty(instance.Ram))
                {
                    string digits = new(instance.Ram.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out int ramValue))
                    {
                        if (ramValue < 64) ramValue *= 1024;
                        ProfileRamSlider.Value = ramValue;
                    }
                }
                string perfTier = instance.PerformanceTier ?? "Auto";
                foreach (ComboBoxItem cbItem in ProfilePerformanceTierComboBox.Items)
                {
                    if (cbItem.Tag?.ToString() == perfTier) { ProfilePerformanceTierComboBox.SelectedItem = cbItem; break; }
                }
                ProfileJavaArgsTextBox.Text = instance.JavaArgs ?? "";
                EditProfileTitle.Text = LocalizationManager.GetString("GAME_PROFILES_EDIT_TITLE", "Edit Profile");
                SaveProfileButton.Content = LocalizationManager.GetString("GAME_PROFILES_SAVE_BTN", "Save");
                ShowEditView();
            }
        }
        private void OpenInstanceFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string instanceId)
            {
                var instance = InstanceManager.Instance.GetInstance(instanceId);
                if (instance != null && Directory.Exists(instance.BasePath))
                    Process.Start("explorer.exe", instance.BasePath);
            }
        }
        private async void DeleteInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string instanceId)
            {
                var instance = InstanceManager.Instance.GetInstance(instanceId);
                if (instance == null) return;
                if (await ShowConfirmAsync($"Are you sure you want to delete profile '{instance.Name}'? This cannot be undone.", "Delete Profile"))
                {
                    if (InstanceManager.Instance.RemoveInstance(instanceId))
                    {
                        LogMessage("Profile deleted.");
                        LoadGameProfilesList();
                        LoadGameProfiles();
                    }
                }
            }
        }
        private async void DuplicateInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string instanceId)
            {
                var instance = InstanceManager.Instance.GetInstance(instanceId);
                if (instance == null) return;
                try
                {
                    string newName = $"{instance.Name} copy";
                    int counter = 2;
                    while (InstanceManager.Instance.GetInstanceByName(newName) != null)
                        newName = $"{instance.Name} copy #{counter++}";
                    var newInstance = InstanceManager.Instance.CreateInstance(newName, instance.Version, instance.ModLoader, instance.Ram);
                    if (newInstance != null)
                    {
                        newInstance.Icon = instance.Icon;
                        CopyInstanceFiles(instance.BasePath, newInstance.BasePath);
                        InstanceManager.Instance.SaveInstances();
                        LogMessage($"Duplicated profile to '{newName}'");
                        LoadGameProfilesList();
                        LoadGameProfiles();
                        await ShowMessageAsync($"Profile duplicated as '{newName}'", "Success");
                    }
                }
                catch (Exception ex) { await ShowMessageAsync($"Error duplicating profile: {ex.Message}"); }
            }
        }
        private void CopyInstanceFiles(string sourcePath, string destPath)
        {
            if (!Directory.Exists(sourcePath)) return;
            string[] subDirs = ["mods", "resourcepacks", "shaderpacks", "saves"];
            foreach (var sub in subDirs)
            {
                string src = Path.Combine(sourcePath, sub);
                string dst = Path.Combine(destPath, sub);
                if (Directory.Exists(src)) CopyDirectory(src, dst);
            }
            string srcOpts = Path.Combine(sourcePath, "options.txt");
            if (File.Exists(srcOpts)) File.Copy(srcOpts, Path.Combine(destPath, "options.txt"), true);
        }
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (string dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
        private async void InstanceIconButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, GetHwnd());
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _tempIconPath = file.Path;
                LogMessage($"Icon selected: {_tempIconPath}");
                try
                {
                    InstanceIconImage.Source = new BitmapImage(new Uri(_tempIconPath));
                }
                catch { }
            }
        }
        private void LoadProfiles()
        {
            try
            {
                ProfileComboBox.Items.Clear();
                var profiles = ProfileManager.Instance.GetProfiles();
                foreach (var profile in profiles)
                    ProfileComboBox.Items.Add(new ComboBoxItem { Content = profile.GetDisplayName(), Tag = profile });
                if (ProfileComboBox.Items.Count > 0)
                {
                    var selectedProfile = ProfileManager.Instance.GetSelectedProfile();
                    if (selectedProfile != null)
                    {
                        for (int i = 0; i < ProfileComboBox.Items.Count; i++)
                        {
                            if (ProfileComboBox.Items[i] is ComboBoxItem cbi && cbi.Tag is UserProfile p && p.Uuid == selectedProfile.Uuid)
                            { ProfileComboBox.SelectedIndex = i; break; }
                        }
                    }
                    else ProfileComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading profiles: {ex.Message}");
                ProfileComboBox.Items.Add(new ComboBoxItem { Content = "Demo User", Tag = new UserProfile { Username = "Demo User", Type = "Demo" } });
                ProfileComboBox.SelectedIndex = 0;
            }
        }
        private void LoadGameProfiles()
        {
            try
            {
                GameProfileComboBox.Items.Clear();
                var instances = InstanceManager.Instance.GetInstances();
                foreach (var instance in instances)
                    GameProfileComboBox.Items.Add(new ComboBoxItem { Content = instance.Name, Tag = instance });
                var profiles = GameProfileManager.Instance.GetProfiles();
                foreach (var profile in profiles)
                    if (!instances.Any(i => i.Name == profile.Name))
                        GameProfileComboBox.Items.Add(new ComboBoxItem { Content = profile.Name, Tag = profile });
                if (GameProfileComboBox.Items.Count > 0)
                {
                    var selectedInstance = InstanceManager.Instance.GetSelectedInstance();
                    var selectedProfile = GameProfileManager.Instance.GetSelectedProfile();
                    if (selectedInstance != null)
                    {
                        for (int i = 0; i < GameProfileComboBox.Items.Count; i++)
                            if (GameProfileComboBox.Items[i] is ComboBoxItem cbi && cbi.Tag is MinecraftInstance inst && inst.InstanceId == selectedInstance.InstanceId)
                            { GameProfileComboBox.SelectedIndex = i; break; }
                    }
                    else if (selectedProfile != null)
                    {
                        for (int i = 0; i < GameProfileComboBox.Items.Count; i++)
                            if (GameProfileComboBox.Items[i] is ComboBoxItem cbi && cbi.Tag is GameProfile prof && prof.Id == selectedProfile.Id)
                            { GameProfileComboBox.SelectedIndex = i; break; }
                    }
                    else GameProfileComboBox.SelectedIndex = 0;
                    UpdateGameProfileInfo();
                }
            }
            catch (Exception ex) { LogMessage($"Error: {ex.Message}"); UpdateGameProfileInfo(); }
        }
        private void UpdateGameProfileInfo()
        {
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance)
                    GameProfileInfo = $"Instance: {instance.Name} | {instance.Version} ({instance.ModLoader})";
                else if (item.Tag is GameProfile profile)
                    GameProfileInfo = $"Profile: {profile.Name} | {profile.Version} ({profile.ModLoader})";
                else
                    GameProfileInfo = "No profile selected";
            }
        }
        private void LoadSettings() { LogMessage("Loading settings..."); }
        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayButton.Content?.ToString() == LocalizationManager.GetString("STOP", "STOP") || PlayButton.Content?.ToString() == "STOP")
            {
                try { _currentProcess?.Kill(); StatusText = "Game killed"; PlayButton.Content = LocalizationManager.GetString("PLAY", "PLAY"); }
                catch (Exception ex) { LogMessage("Failed to stop: " + ex.Message); }
                return;
            }
            if (ProfileComboBox.SelectedItem == null || (ProfileComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content?.ToString() == "Demo User"))
            {
                ManageAccountsButton_Click(null!, null!);
                await ShowMessageAsync("Please add or select an account before playing.", "Account Required");
                return;
            }
            StatusText = "Initializing Launch...";
            PlayButton.IsEnabled = false;
            try
            {
                string version = "1.21.11", ramStr = "2096", javaArgs = "", modLoader = "None";
                string? gameDir = null;
                string performanceTier = "Auto";
                if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
                {
                    if (item.Tag is MinecraftInstance instance)
                    {
                        version = !string.IsNullOrEmpty(instance.InstalledVersionName)
                            ? instance.InstalledVersionName!
                            : instance.Version;
                        ramStr = instance.Ram; javaArgs = instance.JavaArgs;
                        modLoader = instance.ModLoader; gameDir = instance.MinecraftDir;
                        performanceTier = instance.PerformanceTier ?? "Auto";
                    }
                    else if (item.Tag is GameProfile profile)
                    {
                        version = profile.Version; ramStr = profile.Ram;
                        modLoader = profile.ModLoader; gameDir = profile.GameDir;
                    }
                }
                int maxRam = 4096;
                if (!string.IsNullOrEmpty(ramStr))
                {
                    string digits = new(ramStr.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out int val)) maxRam = val < 64 ? val * 1024 : val;
                }
                var session = MSession.CreateOfflineSession("Player");
                var userProfile = ProfileManager.Instance.GetSelectedProfile();
                if (userProfile != null)
                {
                    if (userProfile.Type == "Microsoft")
                    {
                        string tokenToUse = userProfile.MinecraftToken ?? userProfile.AccessToken ?? string.Empty;
                        bool isTokenValid = false;
                        var authManager = new MicrosoftAuthManager();
                        if (!string.IsNullOrEmpty(tokenToUse))
                            isTokenValid = await authManager.ValidateMinecraftToken(tokenToUse);
                        if (!isTokenValid && !string.IsNullOrEmpty(userProfile.RefreshToken))
                        {
                            try
                            {
                                LogMessage("Refreshing session...");
                                var result = await authManager.RefreshTokenAsync(userProfile.RefreshToken);
                                userProfile.MinecraftToken = result.AccessToken;
                                userProfile.RefreshToken = result.RefreshToken;
                                userProfile.AccessToken = result.AccessToken;
                                userProfile.Username = result.Username;
                                userProfile.Uuid = result.Uuid;
                                userProfile.LastUsed = DateTime.Now;
                                ProfileManager.Instance.AddOrUpdateProfile(userProfile);
                                tokenToUse = result.AccessToken;
                                isTokenValid = true;
                                LogMessage("Session refreshed.");
                            }
                            catch (Exception ex) { LogMessage($"Refresh failed: {ex.Message}"); }
                        }
                        session = isTokenValid
                            ? new MSession(userProfile.Username, tokenToUse, userProfile.Uuid)
                            : MSession.CreateOfflineSession(userProfile.Username);
                    }
                    else session = MSession.CreateOfflineSession(userProfile.Username);
                }
                if (DeleteTelemetryCheckBox.IsChecked == true)
                {
                    try
                    {
                        string? targetDir = gameDir;
                        if (string.IsNullOrEmpty(targetDir)) targetDir = PlatformPaths.GetMinecraftDir();
                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            string telemetryPath = Path.Combine(targetDir, "logs", "telemetry");
                            if (Directory.Exists(telemetryPath)) Directory.Delete(telemetryPath, true);
                        }
                    }
                    catch (Exception ex) { LogMessage($"Failed to delete telemetry: {ex.Message}"); }
                }
                LogMessage($"Preparing to launch version: {version} ({modLoader})");
                StatusText = "Checking game files...";
                UpdateDiscordRpc("Playing Minecraft", $"{version} ({modLoader})");
                bool useDiscreteGpu = UseDiscreteGpuCheckBox.IsChecked ?? false;
                _currentProcess = await GameProfileManager.Instance.LaunchGame(version, maxRam, javaArgs, session, modLoader, gameDir, null, null, performanceTier, useDiscreteGpu);
                LogMessage($"===== LAUNCH COMMAND =====");
                LogMessage($"Executable: {_currentProcess.StartInfo.FileName}");
                LogMessage($"Arguments: {_currentProcess.StartInfo.Arguments}");
                LogMessage("==========================");
                _currentProcess.StartInfo.UseShellExecute = false;
                _currentProcess.StartInfo.RedirectStandardOutput = true;
                _currentProcess.StartInfo.RedirectStandardError = true;
                _currentProcess.StartInfo.CreateNoWindow = false;
                _currentProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        DispatcherQueue.TryEnqueue(() => LogMessage($"[GAME] {e.Data}"));
                };
                _currentProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        DispatcherQueue.TryEnqueue(() => LogMessage($"[GAME ERROR] {e.Data}"));
                };
                LogMessage("Starting Minecraft...");
                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();
                StatusText = "Minecraft Running";
                PlayButton.Content = LocalizationManager.GetString("STOP", "STOP");
                PlayButton.IsEnabled = true;
                _ = Task.Run(async () =>
                {
                    await _currentProcess.WaitForExitAsync();
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusText = "Game exited";
                        PlayButton.Content = LocalizationManager.GetString("PLAY", "PLAY");
                        LogMessage("Game exited: " + _currentProcess.ExitCode);
                        _currentProcess = null;
                        UpdateDiscordRpc("Idling in launcher");
                    });
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Launch failed: {ex.Message}");
                StatusText = "Launch failed";
                await ShowMessageAsync($"Failed to launch:\n{ex.Message}", "Launch Error");
                PlayButton.IsEnabled = true;
                PlayButton.Content = LocalizationManager.GetString("PLAY", "PLAY");
                UpdateDiscordRpc("Idling in launcher");
            }
        }
        private void ClearLogButton_Click(object sender, RoutedEventArgs e) => LogTextBox.Text = "";
        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile profile)
            {
                ProfileManager.Instance.SetSelectedProfile(profile.Uuid);
                StatusText = $"Welcome {profile.Username}!";
            }
        }
        private void GameProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance)
                    InstanceManager.Instance.SetSelectedInstance(instance.InstanceId);
                else if (item.Tag is GameProfile profile)
                    GameProfileManager.Instance.SetSelectedProfile(profile.Id);
            }
            UpdateGameProfileInfo();
        }
        private void ProfileRamSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (RamValueText != null)
                RamValueText.Text = $"{(int)e.NewValue} MB";
        }
        private async void ProfileModLoaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileModLoaderComboBox.SelectedItem is ComboBoxItem selected)
            {
                var loaderType = selected.Tag?.ToString()?.ToLower() ?? "";
                if (loaderType == "vanilla")
                {
                    ProfileModLoaderVersionComboBox.ItemsSource = null;
                    ProfileModLoaderVersionComboBox.IsEnabled = false;
                    return;
                }
                ProfileModLoaderVersionComboBox.IsEnabled = true;
                ProfileModLoaderVersionComboBox.ItemsSource = new[] { "Loading..." };
                ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                try
                {
                    var mcVersion = ProfileVersionComboBox.SelectedItem?.ToString() ?? "";
                    var versions = await ModLoaderInstaller.GetAvailableLoadersAsync(mcVersion);
                    var filtered = versions.Where(v => v.LoaderType.Equals(loaderType, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (filtered.Count > 0)
                    {
                        var versionStrings = filtered.Select(v => v.LoaderVersion).Where(v => !string.IsNullOrEmpty(v)).ToList();
                        ProfileModLoaderVersionComboBox.ItemsSource = versionStrings.Count > 0 ? versionStrings : (object)new[] { "Latest" };
                    }
                    else ProfileModLoaderVersionComboBox.ItemsSource = new[] { "Not available for this MC version" };
                    ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    LogMessage($"Error fetching loader versions: {ex.Message}");
                    ProfileModLoaderVersionComboBox.ItemsSource = new[] { "Latest" };
                    ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                }
            }
        }
        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            _isEditingInstance = false;
            _editingInstanceId = null;
            _tempIconPath = null;
            string baseName = "New Instance";
            string name = baseName;
            int counter = 1;
            while (InstanceManager.Instance.GetInstanceByName(name) != null) { counter++; name = $"{baseName} {counter}"; }
            ProfileNameTextBox.Text = name;
            ProfileModLoaderComboBox.SelectedIndex = 0;
            ProfileRamSlider.Value = 4096;
            EditProfileTitle.Text = LocalizationManager.GetString("GAME_PROFILES_CREATE_TITLE", "Create Profile");
            ShowEditView();
        }
        private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = ProfileNameTextBox.Text.Trim();
                string version = ProfileVersionComboBox.SelectedItem?.ToString() ?? "";
                string modLoader = (ProfileModLoaderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.ToLower() ?? "vanilla";
                string loaderVersion = ProfileModLoaderVersionComboBox.SelectedItem?.ToString() ?? "";
                string ram = ((int)ProfileRamSlider.Value).ToString();
                string javaArgs = ProfileJavaArgsTextBox.Text;
                string performanceTier = (ProfilePerformanceTierComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";
                if (string.IsNullOrEmpty(name)) { await ShowMessageAsync("Name empty"); return; }
                string installedVersionName;
                if (modLoader != "vanilla" && !string.IsNullOrEmpty(modLoader))
                {
                    try
                    {
                        SaveProfileButton.IsEnabled = false;
                        StatusText = $"Installing {modLoader}...";
                        var mcPath = PlatformPaths.GetMinecraftDir();
                        var loaderVer = loaderVersion?.Contains("Not available") == true || loaderVersion == "Latest" ? null : loaderVersion;
                        installedVersionName = await ModLoaderInstaller.InstallLoaderAsync(mcPath, version, modLoader, loaderVer);
                        LogMessage($"Installed {modLoader}: {installedVersionName}");
                        NotificationHelper.ShowNotification(
                            LocalizationManager.GetString("NOTIFICATION_INSTALL_COMPLETE", "Installation Complete"),
                            $"{modLoader} {installedVersionName}");
                    }
                    catch (Exception loaderEx)
                    {
                        await ShowMessageAsync($"Failed to install {modLoader}: {loaderEx.Message}", "Error");
                        SaveProfileButton.IsEnabled = true;
                        return;
                    }
                    finally { SaveProfileButton.IsEnabled = true; StatusText = "Ready"; }
                }
                else installedVersionName = version;
                if (!_isEditingInstance)
                {
                    var newInstance = InstanceManager.Instance.CreateInstance(name, installedVersionName, modLoader, ram);
                    if (newInstance != null)
                    {
                        newInstance.JavaArgs = javaArgs;
                        newInstance.Icon = _tempIconPath;
                        newInstance.PerformanceTier = performanceTier;
                        newInstance.Version = installedVersionName;
                        InstanceManager.Instance.SaveInstances();
                        LogMessage($"Created new instance: {name} ({installedVersionName})");
                    }
                }
                else
                {
                    var instance = InstanceManager.Instance.GetInstance(_editingInstanceId!);
                    if (instance != null)
                    {
                        instance.Name = name;
                        instance.Version = installedVersionName;
                        instance.ModLoader = modLoader;
                        instance.Ram = ram;
                        instance.JavaArgs = javaArgs;
                        instance.Icon = _tempIconPath;
                        instance.PerformanceTier = performanceTier;
                        InstanceManager.Instance.SaveInstances();
                        LogMessage($"Updated instance: {name}");
                    }
                }
                LoadGameProfilesList();
                LoadGameProfiles();
                HideEditView();
            }
            catch (Exception ex) { await ShowMessageAsync($"Error saving: {ex.Message}"); }
        }
        private void CancelProfileButton_Click(object sender, RoutedEventArgs e) => HideEditView();
        private void ShowEditView()
        {
            GameProfilesListView.Visibility = Visibility.Collapsed;
            GameProfilesEditView.Visibility = Visibility.Visible;
            ConfigureRamSlider();
            LoadVersionsAsync();
            LoadJavaVersionComboBox();
        }
        private void ConfigureRamSlider()
        {
            try
            {
                long totalRamMb = SystemPerformanceHelper.GetTotalRamMB();
                int maxMb = (int)Math.Max(2048, Math.Min(totalRamMb, int.MaxValue));
                maxMb = (maxMb / 512) * 512;
                if (maxMb < 2048) maxMb = 2048;
                ProfileRamSlider.Maximum = maxMb;
                ProfileRamSlider.Minimum = 1024;
                if (ProfileRamSlider.Value > maxMb) ProfileRamSlider.Value = maxMb;
            }
            catch { }
        }
        private void HideEditView()
        {
            GameProfilesEditView.Visibility = Visibility.Collapsed;
            GameProfilesListView.Visibility = Visibility.Visible;
            _isEditingInstance = false;
        }
        private async void ImportModpackButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, GetHwnd());
            picker.FileTypeFilter.Add(".mrpack");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    var importer = new ModpackImporter();
                    importer.ProgressChanged += (msg, pct) => DispatcherQueue.TryEnqueue(() =>
                    {
                        StatusText = $"{msg} ({pct:F0}%)";
                        LogMessage($"Import: {msg} ({pct:F0}%)");
                    });
                    var (success, message, instanceId) = await importer.ImportMrPackAsync(file.Path);
                    StatusText = message;
                    LogMessage(message);
                    await ShowMessageAsync(message);
                    if (success)
                    {
                        LoadGameProfilesList();
                        if (instanceId != null) { InstanceManager.Instance.SetSelectedInstance(instanceId); LoadGameProfiles(); }
                    }
                }
                catch (Exception ex) { await ShowMessageAsync($"Error: {ex.Message}"); }
            }
        }
        private void LoadGameProfilesList()
        {
            try
            {
                var instances = InstanceManager.Instance.GetInstances();
                _gameInstanceCards.Clear();
                foreach (var inst in instances)
                {
                    _gameInstanceCards.Add(new InstanceCardModel
                    {
                        InstanceId = inst.InstanceId,
                        Name = inst.Name,
                        Version = inst.Version,
                        ModLoader = inst.ModLoader,
                        IconSource = LoadInstanceIconBitmap(inst)
                    });
                }
            }
            catch (Exception ex) { LogMessage($"Error list: {ex.Message}"); }
        }
        private BitmapImage? LoadInstanceIconBitmap(MinecraftInstance instance)
        {
            try
            {
                if (!string.IsNullOrEmpty(instance.Icon) && File.Exists(instance.Icon))
                    return new BitmapImage(new Uri(instance.Icon));
                string iconName = (!string.IsNullOrEmpty(instance.ModLoader) &&
                    !instance.ModLoader.Equals("vanilla", StringComparison.OrdinalIgnoreCase) &&
                    !instance.ModLoader.Equals("none", StringComparison.OrdinalIgnoreCase))
                    ? "minecraft-blue.png" : "minecraft-green.png";
                string[] checkPaths =
                [
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "images", iconName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", iconName),
                ];
                foreach (var path in checkPaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                        return new BitmapImage(new Uri(fullPath));
                }
            }
            catch { }
            return null;
        }
        private async void LoadVersionsAsync()
        {
            try
            {
                var versions = await GameProfileManager.Instance.GetVersions();
                var versionIds = versions.Select(v => v.Id).Distinct().ToList();
                var commonVersions = new[] { "1.21.11", "1.20.1", "1.19.4", "1.19.2", "1.18.2", "1.16.5", "1.12.2", "1.8.9", "1.7.10" };
                foreach (var v in commonVersions)
                    if (!versionIds.Contains(v)) versionIds.Add(v);
                versionIds = versionIds.OrderByDescending(v =>
                {
                    var parts = v.Split('.');
                    if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                    {
                        int patch = parts.Length > 2 && int.TryParse(parts[2], out int p) ? p : 0;
                        return major * 10000 + minor * 100 + patch;
                    }
                    return 0;
                }).ToList();
                ProfileVersionComboBox.ItemsSource = versionIds;
                if (versionIds.Count > 0 && !_isEditingInstance)
                    ProfileVersionComboBox.SelectedIndex = 0;
            }
            catch
            {
                ProfileVersionComboBox.ItemsSource = new[] { "1.20.1", "1.19.4", "1.18.2", "1.16.5", "1.12.2", "1.8.9", "1.7.10" };
                ProfileVersionComboBox.SelectedIndex = 0;
            }
        }
        private void LoadJavaVersionComboBox()
        {
            var items = new List<string> { "Auto (Detect)" };
            foreach (var ver in JavaManager.GetAvailableVersions())
            {
                var label = JavaManager.IsJavaInstalled(ver)
                    ? $"Java {ver} (Installed)"
                    : $"Java {ver}";
                items.Add(label);
            }
            ProfileJavaVersionComboBox.ItemsSource = items;
            ProfileJavaVersionComboBox.SelectedIndex = 0;
        }
        private void InitializeSettings()
        {
            try
            {
                GeneralSettingsButton.Click += (s, e) => ShowSettingsPanel("General");
                AccountsSettingsButton.Click += (s, e) => ShowSettingsPanel("Accounts");
                AdvancedSettingsButton.Click += (s, e) => ShowSettingsPanel("Advanced");
                AboutSettingsButton.Click += (s, e) => ShowSettingsPanel("About");
                LoadSettingsData();
                LoadAccountsData();
                LoadPluginsData();
                LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
                ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
                AddMicrosoftAccountButton.Click += AddMicrosoftAccountButton_Click;
                AddOfflineAccountButton.Click += AddOfflineAccountButton_Click;
                DiscordRpcCheckBox.Checked += (s, e) => { SaveSettings(); InitializeDiscordRpc(); };
                DiscordRpcCheckBox.Unchecked += (s, e) => { SaveSettings(); InitializeDiscordRpc(); };
                DeleteTelemetryCheckBox.Checked += (s, e) => SaveSettings();
                DeleteTelemetryCheckBox.Unchecked += (s, e) => SaveSettings();
                DebugModeCheckBox.Checked += (s, e) => { DebugInfoTextBox.Visibility = Visibility.Visible; SaveSettings(); UpdateDebugInfo(); };
                DebugModeCheckBox.Unchecked += (s, e) => { DebugInfoTextBox.Visibility = Visibility.Collapsed; SaveSettings(); };
                UseDiscreteGpuCheckBox.Checked += (s, e) => SaveSettings();
                UseDiscreteGpuCheckBox.Unchecked += (s, e) => SaveSettings();
                AddPluginButton.Click += AddPluginButton_Click;
                RefreshPluginsButton.Click += (s, e) => { LoadPluginsData(); };
                CheckUpdatesButton.Click += CheckUpdatesButton_Click;
                OpenGitHubButton.Click += (s, e) => UpdateManager.OpenGitHubPage();
                ChangeSkinButton.Click += ChangeSkinButton_Click;
                ResetSkinButton.Click += ResetSkinButton_Click;
                ShowSettingsPanel("General");
                InitializeDiscordRpc();
                LoadJavaList();
                NotificationHelper.Initialize();
            }
            catch (Exception ex) { LogMessage($"Error initializing settings: {ex.Message}"); }
        }
        private void ShowSettingsPanel(string panelName)
        {
            GeneralSettingsPanel.Visibility = Visibility.Collapsed;
            AccountsSettingsPanel.Visibility = Visibility.Collapsed;
            AdvancedSettingsPanel.Visibility = Visibility.Collapsed;
            AboutSettingsPanel.Visibility = Visibility.Collapsed;
            GeneralSettingsButton.Background = null;
            AccountsSettingsButton.Background = null;
            AdvancedSettingsButton.Background = null;
            AboutSettingsButton.Background = null;
            Button activeButton = panelName switch
            {
                "General" => GeneralSettingsButton,
                "Accounts" => AccountsSettingsButton,
                "Advanced" => AdvancedSettingsButton,
                "About" => AboutSettingsButton,
                _ => GeneralSettingsButton
            };
            switch (panelName)
            {
                case "General": GeneralSettingsPanel.Visibility = Visibility.Visible; break;
                case "Accounts":
                    AccountsSettingsPanel.Visibility = Visibility.Visible;
                    _ = LoadCurrentAccountSkinAsync();
                    break;
                case "Advanced": AdvancedSettingsPanel.Visibility = Visibility.Visible; break;
                case "About": AboutSettingsPanel.Visibility = Visibility.Visible; break;
            }
        }
        private void LoadSettingsData()
        {
            try
            {
                var configPath = Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    if (config != null)
                    {
                        if (config.TryGetValue("language", out var lang))
                        {
                            var langCode = lang.GetString() ?? "en-US";
                            foreach (ComboBoxItem item in LanguageComboBox.Items)
                                if (item.Tag?.ToString() == langCode) { LanguageComboBox.SelectedItem = item; break; }
                            LocalizationManager.LoadLanguage(langCode);
                        }
                        if (config.TryGetValue("theme", out var theme))
                        {
                            var themeName = theme.GetString() ?? "dark";
                            foreach (ComboBoxItem item in ThemeComboBox.Items)
                                if (item.Tag?.ToString() == themeName) { ThemeComboBox.SelectedItem = item; break; }
                            ApplyTheme(themeName);
                        }
                        if (config.TryGetValue("discordRpcEnabled", out var rpc))
                            try { DiscordRpcCheckBox.IsChecked = rpc.GetBoolean(); } catch { }
                        if (config.TryGetValue("deleteTelemetryOnStartup", out var tel))
                            try { DeleteTelemetryCheckBox.IsChecked = tel.GetBoolean(); } catch { }
                        if (config.TryGetValue("debugModeEnabled", out var dbg))
                        {
                            try
                            {
                                var val = dbg.GetBoolean();
                                DebugModeCheckBox.IsChecked = val;
                                DebugInfoTextBox.Visibility = val ? Visibility.Visible : Visibility.Collapsed;
                            }
                            catch { }
                        }
                        if (config.TryGetValue("useDiscreteGpu", out var gpu))
                            try { UseDiscreteGpuCheckBox.IsChecked = gpu.GetBoolean(); } catch { }
                    }
                }
                else
                {
                    LanguageComboBox.SelectedIndex = 0;
                    ThemeComboBox.SelectedIndex = 0;
                    DiscordRpcCheckBox.IsChecked = true;
                }
            }
            catch { }
        }
        private void LoadAccountsData()
        {
            try
            {
                _userProfiles.Clear();
                var profiles = ProfileManager.Instance.GetProfiles();
                foreach (var profile in profiles) _userProfiles.Add(profile);
                var displayItems = profiles.Select(p => new UserProfileDisplay
                {
                    Username = p.Username,
                    TypeDisplay = p.Type,
                    Profile = p
                }).ToList();
                AccountsListBox.ItemsSource = displayItems;
            }
            catch { }
        }
        private void LoadPluginsData()
        {
            try
            {
                _plugins.Clear();
                var pluginsPath = Path.Combine(PlatformPaths.GetDataDir(), "launcher_plugins.json");
                if (File.Exists(pluginsPath))
                {
                    var json = File.ReadAllText(pluginsPath);
                    var plugins = JsonSerializer.Deserialize<List<PluginInfo>>(json);
                    if (plugins != null) foreach (var p in plugins) _plugins.Add(p);
                }
                PluginsListBox.ItemsSource = _plugins;
                PluginManager.Instance.SetLogAction(msg => DispatcherQueue.TryEnqueue(() => LogMessage(msg)));
                PluginManager.Instance.LoadPlugins(_plugins);
            }
            catch { }
        }
        private void SaveSettings()
        {
            try
            {
                var configPath = Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json");
                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                var config = new Dictionary<string, object>
                {
                    ["language"] = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en-US",
                    ["theme"] = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "dark",
                    ["discordRpcEnabled"] = DiscordRpcCheckBox.IsChecked ?? true,
                    ["deleteTelemetryOnStartup"] = DeleteTelemetryCheckBox.IsChecked ?? false,
                    ["debugModeEnabled"] = DebugModeCheckBox.IsChecked ?? false,
                    ["useDiscreteGpu"] = UseDiscreteGpuCheckBox.IsChecked ?? false
                };
                File.WriteAllText(configPath, JsonSerializer.Serialize(config, _jsonSerializerOptions));
            }
            catch { }
        }
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string languageCode)
            {
                SaveSettings();
                LocalizationManager.LoadLanguage(languageCode);
                ApplyLocalization();
            }
        }
        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string themeKey)
            {
                ApplyTheme(themeKey);
                SaveSettings();
            }
        }
        private async void AddMicrosoftAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new MicrosoftLoginDialog();
            loginWindow.Activate();
            var confirmed = await loginWindow.WaitForResultAsync();
            if (confirmed && loginWindow.ResultProfile != null)
            {
                ProfileManager.Instance.AddOrUpdateProfile(loginWindow.ResultProfile);
                LoadAccountsData();
                LoadProfiles();
                StatusText = $"Logged in as {loginWindow.ResultProfile.Username}";
            }
        }
        private async void AddOfflineAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Add Offline Account",
                Content = new TextBox { PlaceholderText = "Enter offline username" },
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                XamlRoot = Content.XamlRoot
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var username = ((TextBox)dialog.Content).Text?.Trim();
                if (!string.IsNullOrWhiteSpace(username))
                {
                    ProfileManager.Instance.AddProfile(username, "Offline");
                    LoadAccountsData();
                    LoadProfiles();
                }
            }
        }
        private async void AddPluginButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, GetHwnd());
            picker.FileTypeFilter.Add(".dll");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _plugins.Add(new PluginInfo { Path = file.Path, IsEnabled = true });
                SavePlugins();
                PluginManager.Instance.LoadPlugins(_plugins);
            }
        }
        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckUpdatesButton.IsEnabled = false;
                StatusText = "Checking for updates...";
                var updateInfo = await UpdateManager.CheckForUpdatesAsync();
                string releaseLabel = string.IsNullOrWhiteSpace(updateInfo.ReleaseName)
                    ? updateInfo.NewVersion
                    : $"{updateInfo.ReleaseName} ({updateInfo.NewVersion})";
                if (updateInfo.UpdateAvailable)
                {
                    await ShowMessageAsync(
                        $"A new version is available.\n\nCurrent version: {updateInfo.CurrentVersion}\nLatest release: {releaseLabel}\n\nOpening the GitHub releases page so you can download it manually.",
                        "Update Available");
                    UpdateManager.OpenReleasesPage();
                }
                else
                {
                    await ShowMessageAsync(
                        $"You are running the latest version.\n\nCurrent: {updateInfo.CurrentVersion}\nLatest release: {releaseLabel}",
                        "No Updates");
                }
                StatusText = "Ready";
            }
            catch (Exception ex) { await ShowMessageAsync($"Failed to check for updates: {ex.Message}"); StatusText = "Ready"; }
            finally { CheckUpdatesButton.IsEnabled = true; }
        }
        private void AccountsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => _ = LoadCurrentAccountSkinAsync();
        private async Task LoadCurrentAccountSkinAsync()
        {
            UserProfile? currentProfile = null;
            if (AccountsListBox.SelectedItem is UserProfileDisplay display) currentProfile = display.Profile;
            else if (AccountsListBox.SelectedItem is UserProfile profile) currentProfile = profile;
            else if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile cp) currentProfile = cp;
            if (currentProfile == null || string.IsNullOrEmpty(currentProfile.Username))
            {
                SkinInfoText.Text = "Select an account to view skin.";
                var _root = this.Content as Microsoft.UI.Xaml.FrameworkElement;
                var _skinFullImg = _root?.FindName("SkinFullImage") as Microsoft.UI.Xaml.Controls.Image;
                if (_skinFullImg != null) _skinFullImg.Source = null;
                return;
            }
            try
            {
                SkinInfoText.Text = "Loading skin...";
                PlayerSkinInfo? skinInfo = null;
                if (currentProfile.Type == "Microsoft")
                {
                    var token = currentProfile.MinecraftToken ?? currentProfile.AccessToken;
                    if (!string.IsNullOrEmpty(token))
                        skinInfo = await SkinManager.GetPlayerSkinWithTokenAsync(token);
                }
                skinInfo ??= await SkinManager.GetPlayerSkinAsync(currentProfile.Username);
                if (skinInfo != null)
                {
                    LogMessage($"SkinInfo retrieved: url={(string.IsNullOrEmpty(skinInfo.SkinUrl) ? "<none>" : skinInfo.SkinUrl)}, isSlim={skinInfo.IsSlim}, SkinImage={(skinInfo.SkinImage != null)}, HeadImage={(skinInfo.HeadImage != null)}");
                    var _root = this.Content as Microsoft.UI.Xaml.FrameworkElement;
                    var _skinFullImg = _root?.FindName("SkinFullImage") as Microsoft.UI.Xaml.Controls.Image;
                    BitmapImage? preferred = skinInfo.SkinImage ?? skinInfo.HeadImage;
                    var fullRender = await SkinManager.GetBodyRenderAsync(skinInfo.Uuid, 12);
                    LogMessage($"GetBodyRenderAsync returned {(fullRender != null ? "OK" : "null")}");
                    BitmapImage? head3d = null;
                    if (fullRender == null) head3d = await SkinManager.GetHead3DAsync(skinInfo.Uuid, 160);
                    LogMessage($"GetHead3DAsync returned {(head3d != null ? "OK" : "null")}");
                    BitmapImage? head2d = null;
                    if (fullRender == null && head3d == null) head2d = await SkinManager.GetHeadAvatarAsync(skinInfo.Uuid, 160);
                    LogMessage($"GetHeadAvatarAsync returned {(head2d != null ? "OK" : "null")}");
                    BitmapImage? headCrop = null;
                    if (fullRender == null && head3d == null && head2d == null && !string.IsNullOrEmpty(skinInfo.SkinUrl)) headCrop = await SkinManager.GetHeadCropAsync(skinInfo.SkinUrl, 160);
                    LogMessage($"GetHeadCropAsync returned {(headCrop != null ? "OK" : "null")}");
                    BitmapImage? frontComposite = null;
                    if (fullRender == null && head3d == null && head2d == null && headCrop == null && !string.IsNullOrEmpty(skinInfo.SkinUrl)) frontComposite = await SkinManager.GetFrontCompositeAsync(skinInfo.SkinUrl, 160);
                    LogMessage($"GetFrontCompositeAsync returned {(frontComposite != null ? "OK" : "null")}");
                    var toShow = preferred ?? fullRender ?? head3d ?? head2d ?? headCrop ?? frontComposite;
                    if (toShow == null && !string.IsNullOrEmpty(skinInfo.SkinUrl))
                    {
                        LogMessage($"Attempting direct download from skin URL...");
                        try { toShow = await SkinManager.GetSkinImageAsync(skinInfo.SkinUrl); LogMessage(toShow != null ? "Direct download OK" : "Direct download failed"); }
                        catch (Exception ex) { LogMessage($"Direct download exception: {ex.Message}"); }
                    }
                    LogMessage(toShow != null ? $"Skin preview: using bitmap source for {skinInfo.Username}" : $"Skin preview: no preview available for {skinInfo.Username}");
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (_skinFullImg != null)
                        {
                            BitmapImage? uiImg = null;
                            try
                            {
                                if (toShow?.UriSource != null)
                                {
                                    uiImg = new BitmapImage(toShow.UriSource);
                                }
                                else
                                {
                                    uiImg = toShow;
                                }
                            }
                            catch { uiImg = toShow; }
                            _skinFullImg.Source = uiImg;
                            _skinFullImg.Visibility = uiImg != null ? Visibility.Visible : Visibility.Collapsed;
                        }
                        if (skinInfo.IsSlim) SkinModelAlex.IsChecked = true;
                        else SkinModelSteve.IsChecked = true;
                        SkinInfoText.Text = $"Username: {skinInfo.Username}\nUUID: {skinInfo.Uuid}\nModel: {(skinInfo.IsSlim ? "Alex (slim)" : "Steve (classic)")}";
                    });
                }
                else { SkinInfoText.Text = "Could not find player or skin data."; }
            }
            catch (Exception ex) { SkinInfoText.Text = $"Error: {ex.Message}"; }
        }
        private async void ChangeSkinButton_Click(object sender, RoutedEventArgs e)
        {
            UserProfile? msAccount = GetMicrosoftAccount();
            if (msAccount == null || string.IsNullOrEmpty(msAccount.AccessToken))
            {
                await ShowMessageAsync("You need to log in with a Microsoft account to change your skin.", "Login Required");
                return;
            }
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, GetHwnd());
            picker.FileTypeFilter.Add(".png");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    ChangeSkinButton.IsEnabled = false;
                    SkinInfoText.Text = "Uploading skin...";
                    var success = await SkinManager.UploadSkinAsync(msAccount.AccessToken, file.Path, true);
                    SkinInfoText.Text = success ? "Skin uploaded successfully!" : "Failed to upload skin.";
                    if (success) { await Task.Delay(2000); await LoadCurrentAccountSkinAsync(); }
                }
                catch (Exception ex) { SkinInfoText.Text = $"Error: {ex.Message}"; }
                finally { ChangeSkinButton.IsEnabled = true; }
            }
        }
        private async void ResetSkinButton_Click(object sender, RoutedEventArgs e)
        {
            UserProfile? msAccount = GetMicrosoftAccount();
            if (msAccount == null || string.IsNullOrEmpty(msAccount.AccessToken))
            {
                await ShowMessageAsync("You need to log in with a Microsoft account to reset your skin.", "Login Required");
                return;
            }
            if (!await ShowConfirmAsync("Are you sure you want to reset your skin to the default?", "Reset Skin"))
                return;
            try
            {
                ResetSkinButton.IsEnabled = false;
                SkinInfoText.Text = "Resetting skin...";
                var success = await SkinManager.ResetSkinAsync(msAccount.AccessToken);
                SkinInfoText.Text = success ? "Skin reset successfully!" : "Failed to reset skin.";
                if (success) { await Task.Delay(2000); await LoadCurrentAccountSkinAsync(); }
            }
            catch (Exception ex) { SkinInfoText.Text = $"Error: {ex.Message}"; }
            finally { ResetSkinButton.IsEnabled = true; }
        }
        private UserProfile? GetMicrosoftAccount()
        {
            if (AccountsListBox.SelectedItem is UserProfileDisplay display && display.Profile.Type == "Microsoft") return display.Profile;
            if (AccountsListBox.SelectedItem is UserProfile p && p.Type == "Microsoft") return p;
            if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile cp && cp.Type == "Microsoft") return cp;
            return ProfileManager.Instance.GetProfiles().FirstOrDefault(p => p.Type == "Microsoft");
        }
        private void SavePlugins()
        {
            try
            {
                var path = Path.Combine(PlatformPaths.GetDataDir(), "launcher_plugins.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(_plugins.ToList(), _jsonSerializerOptions));
            }
            catch { }
        }
        private void LoadJavaList()
        {
            try
            {
                var items = new List<JavaListItem>();
                foreach (var version in JavaManager.GetAvailableVersions())
                {
                    var installed = JavaManager.IsJavaInstalled(version);
                    items.Add(new JavaListItem
                    {
                        MajorVersion = version,
                        DisplayName = $"Java {version} (Adoptium Temurin)",
                        StatusText = installed
                            ? LocalizationManager.GetString("JAVA_INSTALLED", "Installed")
                            : LocalizationManager.GetString("JAVA_NOT_INSTALLED", "Not Installed"),
                        InstallButtonText = installed
                            ? LocalizationManager.GetString("JAVA_INSTALLED", "Installed")
                            : LocalizationManager.GetString("JAVA_INSTALL", "Install"),
                        IsInstalled = installed,
                        CanInstall = !installed
                    });
                }
                JavaListBox.ItemsSource = items;
            }
            catch (Exception ex) { LogMessage($"Error loading Java list: {ex.Message}"); }
        }
        private void RefreshJavaButton_Click(object sender, RoutedEventArgs e) => LoadJavaList();
        private async void JavaInstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int majorVersion)
            {
                btn.IsEnabled = false;
                JavaProgressBar.Visibility = Visibility.Visible;
                JavaProgressBar.IsIndeterminate = true;
                JavaManager.OnProgressChanged += OnJavaProgress;
                JavaManager.OnProgressPercentChanged += OnJavaPercent;
                try
                {
                    await JavaManager.InstallJavaAsync(majorVersion);
                    var successMsg = string.Format(
                        LocalizationManager.GetString("JAVA_INSTALL_SUCCESS", "Java {0} installed successfully!"),
                        majorVersion);
                    JavaProgressText.Text = successMsg;
                    NotificationHelper.ShowNotification(
                        LocalizationManager.GetString("NOTIFICATION_INSTALL_COMPLETE", "Installation Complete"),
                        string.Format(LocalizationManager.GetString("NOTIFICATION_JAVA_INSTALLED", "Java {0} has been installed successfully."), majorVersion));
                    LoadJavaList();
                }
                catch (Exception ex)
                {
                    JavaProgressText.Text = $"Error: {ex.Message}";
                    await ShowMessageAsync($"Failed to install Java {majorVersion}: {ex.Message}", "Error");
                }
                finally
                {
                    JavaManager.OnProgressChanged -= OnJavaProgress;
                    JavaManager.OnProgressPercentChanged -= OnJavaPercent;
                    JavaProgressBar.IsIndeterminate = false;
                    JavaProgressBar.Visibility = Visibility.Collapsed;
                    btn.IsEnabled = true;
                }
            }
        }
        private async void JavaUninstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int majorVersion)
            {
                if (await ShowConfirmAsync($"Uninstall Java {majorVersion}?", "Confirm"))
                {
                    try
                    {
                        JavaManager.UninstallJava(majorVersion);
                        JavaProgressText.Text = string.Format(
                            LocalizationManager.GetString("JAVA_UNINSTALL_SUCCESS", "Java {0} uninstalled."),
                            majorVersion);
                        LoadJavaList();
                    }
                    catch (Exception ex) { await ShowMessageAsync($"Error: {ex.Message}"); }
                }
            }
        }
        private void OnJavaProgress(string msg)
        {
            DispatcherQueue.TryEnqueue(() => JavaProgressText.Text = msg);
        }
        private void OnJavaPercent(int pct)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                JavaProgressBar.IsIndeterminate = false;
                JavaProgressBar.Value = pct;
                JavaProgressBar.Maximum = 100;
            });
        }
        private void UpdateDebugInfo()
        {
            if (DebugInfoTextBox.Visibility == Visibility.Visible)
                DebugInfoTextBox.Text = $"OS: {Environment.OSVersion}\n.NET: {Environment.Version}\nData: {PlatformPaths.GetDataDir()}";
        }
        private void RestartApplication()
        {
            var path = Environment.ProcessPath;
            if (path != null) { Process.Start(path); Application.Current.Exit(); }
        }
        private void LogMessage(string message)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LogTextBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            });
        }
        private async Task ShowMessageAsync(string message, string title = "")
        {
            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(title) ? "OrangLauncher" : title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        private async Task<bool> ShowConfirmAsync(string message, string title = "")
        {
            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(title) ? "Confirm" : title,
                Content = message,
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = Content.XamlRoot
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
    }
}