using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using OrangLauncher.Managers;
using OrangLauncher.Models;
using OrangLauncher.Pages;
using OrangLauncher.ViewModels;
using System.Globalization;
namespace OrangLauncher
{
    public class InstanceIconConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is MinecraftInstance instance ? InstanceIcons.For(instance, 64) : null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is string path ? System.IO.Path.GetFileName(path) : value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class Base64ImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string base64 && !string.IsNullOrEmpty(base64))
            {
                try
                {
                    if (base64.StartsWith("data:image/png;base64,")) base64 = base64["data:image/png;base64,".Length..];
                    byte[] binaryData = System.Convert.FromBase64String(base64);
                    BitmapImage bi = new();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = new MemoryStream(binaryData);
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
                catch { }
            }
            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class JavaVersionViewModel
    {
        public int Version { get; set; }
        public string Label { get; set; } = "";
        public string Status { get; set; } = "";
        public string InstallButtonText { get; set; } = "Install";
        public string UninstallButtonText { get; set; } = "Uninstall";
        public bool ShowInstall { get; set; }
        public bool ShowUninstall { get; set; }
    }
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public static MainWindow? Current { get; private set; }
        private static string L(string key, string fallback) => LocalizationManager.GetString(key, fallback);
        private static string L(string key, string fallback, params (string, object?)[] args) => LocalizationManager.T(key, fallback, args);
        private string _statusText = "";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
        private string _gameProfileInfo = "";
        public string GameProfileInfo { get => _gameProfileInfo; set { _gameProfileInfo = value; OnPropertyChanged(); } }
        public string LocalizedUpdateNotes => L("UPDATE_NOTES", "Update Notes");
        public string LocalizedLauncherLog => L("LAUNCHER_LOG", "Launcher Log");
        public string LocalizedGameProfiles => L("GAME_PROFILES", "Game Profiles");
        public string LocalizedMods => L("MODS", "Mods");
        public string LocalizedResourcePacks => L("RESOURCE_PACKS", "Resource & Shader Packs");
        public string LocalizedSettings => L("SETTINGS", "Settings");
        public string LocalizedProfile => L("PROFILE", "PROFILE");
        public string LocalizedGameProfilesTitle => L("GAME_PROFILES_TITLE", "GAME PROFILES");
        public string LocalizedPlay => L("PLAY", "PLAY");
        public string LocalizedStop => L("STOP", "STOP");
        public string LocalizedNewProfile => L("NEW_PROFILE", "NEW PROFILE");
        public string LocalizedGeneral => L("GENERAL", "General");
        public string LocalizedAccounts => L("ACCOUNTS", "Accounts");
        public string LocalizedAdvanced => L("ADVANCED", "Advanced");
        public string LocalizedAbout => L("ABOUT", "About");
        public string LocalizedClearLog => L("CLEAR_LOG", "Clear Log");
        public string LocalizedSelect => L("SELECT", "Select");
        public string LocalizedDuplicate => L("DUPLICATE", "Duplicate");
        public string LocalizedOpenFolder => L("OPEN_FOLDER", "Open Folder");
        public string LocalizedInstanceSelect => L("INSTANCE_SELECT", "Select");
        public string LocalizedInstanceEdit => L("INSTANCE_EDIT", "Edit");
        public string LocalizedInstanceDuplicate => L("INSTANCE_DUPLICATE", "Duplicate");
        public string LocalizedInstanceOpenFolder => L("INSTANCE_OPEN_FOLDER", "Open Folder");
        public string LocalizedInstanceExport => L("QT_EXPORT", "Export");
        public string LocalizedInstanceDelete => L("INSTANCE_DELETE", "Delete");
        public string LocalizedCreateProfile => L("GAME_PROFILES_CREATE_TITLE", "Create Profile");
        public string LocalizedName => L("GAME_PROFILES_NAME", "Name");
        public string LocalizedVersion => L("GAME_PROFILES_VERSION", "Version");
        public string LocalizedModLoader => L("GAME_PROFILES_LOADER", "Mod Loader");
        public string LocalizedLoaderVersion => L("GAME_PROFILES_LOADER_VERSION", "Loader Version");
        public string LocalizedRam => L("GAME_PROFILES_RAM", "RAM");
        public string LocalizedJavaArgs => L("STARTUP_ARGS", "Startup Arguments");
        public string LocalizedSave => L("GAME_PROFILES_SAVE_BTN", "Save");
        public string LocalizedLanguage => L("LANGUAGE", "Language");
        public string LocalizedTheme => L("SETTINGS_CARD_THEME", "Theme");
        public string LocalizedEnableDiscordRpc => L("SETTINGS_DISCORD_ENABLE", "Enable Discord Rich Presence");
        public string LocalizedAddMicrosoftAccount => L("SETTINGS_ACCOUNT_ADD_MS", "Add Microsoft Account");
        public string LocalizedAddOfflineAccount => L("SETTINGS_ACCOUNT_ADD_OFFLINE", "Add Offline Account");
        public string LocalizedRemove => L("REMOVE", "Remove");
        public string LocalizedPerformance => L("PERFORMANCE", "Performance Tier");
        public string LocalizedCheckForUpdates => L("SETTINGS_ABOUT_CHECK_UPDATES", "Check for Updates");
        public string LocalizedViewOnGitHub => L("SETTINGS_ABOUT_GITHUB", "View on GitHub");
        public string LocalizedResourcePacksTab => L("RES_SH_RP_TITLE", "Resource Packs");
        public string LocalizedShaderPacksTab => L("RES_SH_SP_TITLE", "Shader Packs");
        public string LocalizedModdingTab => L("MODS_TAB_TITLE", "Modding");
        public string LocalizedUpdate => L("UPDATE", "Update");
        public string LocalizedServerManager => L("SERVER_MANAGER", "Server Manager");
        public string LocalizedGeneralSettings => L("SETTINGS_GENERAL_TITLE", "General Settings");
        public string LocalizedAccountManagement => L("SETTINGS_ACCOUNTS_TITLE", "Account Management");
        public string LocalizedAdvancedSettings => L("SETTINGS_ADVANCED_TITLE", "Advanced Settings");
        public string LocalizedLanguageDesc => L("SETTINGS_LANGUAGE_DESC", "Choose your preferred language for the launcher interface.");
        public string LocalizedLanguageNote => L("SETTINGS_LANGUAGE_NOTE", "Note: Changing language requires restarting the application.");
        public string LocalizedThemeDesc => L("SETTINGS_THEME_DESC", "Choose your preferred visual theme.");
        public string LocalizedThemeNote => L("SETTINGS_THEME_NOTE", "Theme changes apply immediately.");
        public string LocalizedMinecraftAccounts => L("SETTINGS_MC_ACCOUNTS", "Minecraft Accounts");
        public string LocalizedMinecraftAccountsDesc => L("SETTINGS_MC_ACCOUNTS_DESC", "Manage your Minecraft accounts for launching games.");
        public string LocalizedSkinPreview => L("SETTINGS_SKIN_PREVIEW", "Skin Preview");
        public string LocalizedSkinPreviewDesc => L("SETTINGS_SKIN_PREVIEW_DESC", "View and change your Minecraft skin. Select an account above to preview their skin.");
        public string LocalizedSelectAccountToView => L("SETTINGS_SELECT_ACCOUNT_VIEW", "Select an account to view skin");
        public string LocalizedUploadSkin => L("SETTINGS_UPLOAD_SKIN", "Upload Skin");
        public string LocalizedResetSkin => L("SETTINGS_RESET_SKIN", "Reset Skin");
        public string LocalizedSaveSkin => L("QT_SAVE_SKIN", "Save skin as PNG...");
        public string LocalizedCape => L("QT_CAPE", "Cape:");
        public string LocalizedModel => L("SETTINGS_MODEL", "Model:");
        public string LocalizedSteveClassic => L("SETTINGS_STEVE", "Steve (Classic)");
        public string LocalizedAlexSlim => L("SETTINGS_ALEX", "Alex (Slim)");
        public string LocalizedDiscordRichPresence => L("SETTINGS_DISCORD_TITLE", "Discord Rich Presence");
        public string LocalizedDiscordRpcDesc => L("SETTINGS_DISCORD_DESC", "Show your Minecraft activity in Discord.");
        public string LocalizedTelemetry => L("SETTINGS_TELEMETRY_TITLE", "Telemetry");
        public string LocalizedDeleteTelemetryFiles => L("SETTINGS_TELEMETRY_ENABLE", "Delete telemetry files on startup");
        public string LocalizedTelemetryDesc => L("SETTINGS_TELEMETRY_DESC", "Automatically clean up Minecraft telemetry data on launcher startup.");
        public string LocalizedTitleBar => L("SETTINGS_TITLEBAR_TITLE", "Title bar");
        public string LocalizedModernTitleBar => L("SETTINGS_TITLEBAR_MODERN", "Use the modern Windows 11 title bar");
        public string LocalizedModernTitleBarDesc => L("SETTINGS_TITLEBAR_DESC", "Native rounded title bar that follows the launcher theme, with the help (?) button next to the window buttons. Applies after a restart.");
        public string LocalizedPlugins => L("SETTINGS_PLUGINS_TITLE", "Plugins");
        public string LocalizedPluginsDesc => L("SETTINGS_PLUGINS_DESC", "Manage launcher plugins and extensions.");
        public string LocalizedAddPlugin => L("SETTINGS_ADD_PLUGIN", "Add Plugin");
        public string LocalizedRefresh => L("REFRESH", "Refresh");
        public string LocalizedDebugMode => L("SETTINGS_DEBUG_TITLE", "Debug Mode");
        public string LocalizedEnableDebugMode => L("SETTINGS_DEBUG_ENABLE", "Enable debug mode");
        public string LocalizedDebugModeDesc => L("SETTINGS_DEBUG_DESC", "Show detailed logging and debugging information.");
        public string LocalizedGpuSettings => L("SETTINGS_GPU_TITLE", "GPU Settings");
        public string LocalizedForceDiscreteGpu => L("SETTINGS_GPU_ENABLE", "Force use discrete GPU (NVIDIA/AMD)");
        public string LocalizedGpuDesc => L("SETTINGS_GPU_DESC", "Forces Minecraft to use your dedicated graphics card instead of integrated graphics. Improves performance on laptops with multiple GPUs.");
        public string LocalizedNewsBrowser => L("SETTINGS_BROWSER_TITLE", "News Browser");
        public string LocalizedNewsBrowserDesc => L("SETTINGS_BROWSER_DESC", "Choose the browser engine for displaying news.");
        public string LocalizedNewsBrowserNote => L("SETTINGS_BROWSER_NOTE", "WebView2 provides modern features, IE for compatibility.");
        public string LocalizedJavaInstallation => L("SETTINGS_JAVA_TITLE", "Java Installation");
        public string LocalizedJavaInstallationDesc => L("SETTINGS_JAVA_DESC", "Install and manage Java versions for Minecraft. Different versions require different Java runtimes.");
        public string LocalizedJava32Bit => L("SETTINGS_JAVA_X86", "Use 32-bit (x86) Java instead of 64-bit");
        public string LocalizedJava32BitDesc => L("SETTINGS_JAVA_X86_DESC", "For compatibility with older systems or graphics drivers. 32-bit builds are only available for Java 8 and 17, other versions fall back to 64-bit automatically.");
        public string LocalizedJavaVersion => L("GAME_PROFILES_JAVA", "Java Version");
        public string LocalizedProxy => L("QT_PROXY", "Network proxy");
        public string LocalizedProxyDesc => L("QT_PROXY_DESC", "Route launcher downloads through a proxy, for example http://host:port or socks5://host:port. Leave empty for a direct connection.");
        public string LocalizedApply => L("QT_APPLY", "Apply");
        public string LocalizedTest => L("QT_TEST", "Test");
        public string LocalizedExternalPrograms => L("QT_EXTERNAL_PROGRAMS", "External programs");
        public string LocalizedExternalProgramsDesc => L("QT_EXTERNAL_PROGRAMS_DESC", "Optional commands used to open links and text files. Leave empty to use the system defaults.");
        public string LocalizedCustomBrowser => L("QT_CUSTOM_BROWSER", "Web browser command");
        public string LocalizedCustomEditor => L("QT_CUSTOM_EDITOR", "Text editor command");
        public string LocalizedSharedFiles => L("SETTINGS_SHARED_FILES", "Shared Files");
        public string LocalizedSharedFilesDesc => L("SETTINGS_SHARED_FILES_DESC_WIN", "Share between all instances. Folders are linked (directory junctions), files are synced before and after every launch.");
        public string LocalizedShareResourcePacks => L("SETTINGS_SHARED_RESOURCEPACKS", "Share resourcepacks/");
        public string LocalizedShareResourcePacksDesc => L("SETTINGS_SHARED_RESOURCEPACKS_DESC", "Texture packs visible in all instances");
        public string LocalizedShareShaderPacks => L("SETTINGS_SHARED_SHADERPACKS", "Share shaderpacks/");
        public string LocalizedShareShaderPacksDesc => L("SETTINGS_SHARED_SHADERPACKS_DESC", "Shader packs visible in all instances");
        public string LocalizedShareOptions => L("SETTINGS_SHARED_OPTIONS_TXT", "Share options.txt");
        public string LocalizedShareOptionsDesc => L("SETTINGS_SHARED_OPTIONS_DESC", "Key bindings, video settings, mouse sensitivity");
        public string LocalizedShareServers => L("SETTINGS_SHARED_SERVERS", "Share servers.dat");
        public string LocalizedShareServersDesc => L("SETTINGS_SHARED_SERVERS_DESC", "Saved multiplayer server list");
        public string LocalizedShareScreenshots => L("SETTINGS_SHARED_SCREENSHOTS", "Share screenshots/");
        public string LocalizedShareScreenshotsDesc => L("SETTINGS_SHARED_SCREENSHOTS_DESC", "Screenshots from every instance in one folder");
        public string LocalizedSharedApplyAll => L("SETTINGS_SHARED_APPLY_ALL", "Apply to all instances");
        public string LocalizedOpenSharedFolder => L("QT_OPEN_SHARED", "Open shared folder");
        public string LocalizedShowSnapshots => L("QT_SHOW_SNAPSHOTS", "Snapshots");
        public string LocalizedShowBetas => L("QT_SHOW_BETAS", "Betas");
        public string LocalizedShowAlphas => L("QT_SHOW_ALPHAS", "Alphas");
        public string LocalizedVersionTypes => L("QT_VERSION_TYPES", "Minecraft versions");
        public string LocalizedVersionTypesDesc => L("QT_VERSION_TYPES_DESC", "Choose which kinds of Minecraft versions appear in the version lists.");
        public string LocalizedAccentColor => L("QT_ACCENT_COLOR", "Accent color");
        public string LocalizedAccentColorDesc => L("QT_ACCENT_COLOR_DESC", "Color of highlighted buttons, switches and selections. Pick a preset or type a hex color like #FF8C00.");
        public string LocalizedGameBarTitle => L("SETTINGS_GAMEBAR_TITLE", "Game Bar popup");
        public string LocalizedGameBarEnable => L("SETTINGS_GAMEBAR_ENABLE", "Hide the \"ms-gamingoverlay\" popup");
        public string LocalizedGameBarDesc => L("SETTINGS_GAMEBAR_DESC", "Stops Windows from asking for an app to open 'ms-gamingoverlay' links when a game starts. Turns off Game Bar game capture for your user. Turn it off to restore the Windows defaults.");
        public string LocalizedAboutVersion => UpdateManager.GetAboutVersionText(L("SETTINGS_ABOUT_VERSION", "Version:"));
        public string LocalizedAboutDesc => L("SETTINGS_ABOUT_DESC", "A modern Legacy Minecraft launcher with advanced features.");
        public string LocalizedDevelopedBy => L("SETTINGS_ABOUT_DEVS", "Developed by: adasjusk and previously vakarux");
        public string LocalizedSearch => L("SEARCH", "Search:");
        public string LocalizedAddPack => L("ADD_PACK", "Add Pack");
        public string LocalizedAddShader => L("ADD_SHADER", "Add Shader");
        public string LocalizedAddMod => L("ADD_MOD", "Add Mod");
        public string LocalizedRemoveSelected => L("REMOVE_SELECTED", "Remove Selected");
        public string LocalizedImportMrpack => L("IMPORT_MRPACK", "Import .mrpack");
        public string LocalizedImportOrangpack => L("IMPORT_ORANGPACK", "Import .orangpack");
        public string LocalizedCancel => L("CANCEL", "Cancel");
        public string LocalizedAddServer => L("ADD_SERVER", "Add Server");
        public string LocalizedQuickPlay => L("QUICK_PLAY", "Quick Play");
        public string LocalizedHidden => L("HIDDEN", "Hidden");
        public string LocalizedManageAccounts => L("MANAGE_ACCOUNTS", "Manage Accounts");
        private readonly ObservableCollection<MinecraftInstance> _gameInstances = [];
        public ObservableCollection<MinecraftInstance> GameInstances => _gameInstances;
        private readonly ObservableCollection<UserProfile> _userProfiles = [];
        private readonly ObservableCollection<PluginInfo> _plugins = [];
        private List<ModInfo> _allMods = [];
        private List<ResourcePackInfo> _allResourcePacks = [];
        private List<ResourcePackInfo> _allShaderPacks = [];
        private DiscordRpcManager? _discordRpcManager;
        private string? _tempIconPath;
        private bool _filling;
        private bool _settingsReady;
        private bool _refreshQueued;
        private string _playMode = "play";
        private LaunchService? _launch;
        private InstanceEditor? _editor;
        private SkinData? _skinData;
        private readonly object _debugLock = new();
        private readonly string _debugLog = System.IO.Path.Combine(LaunchService.LogsDir, $"launcher_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        private readonly List<string> _pendingLog = new();
        private readonly DispatcherTimer _logTimer;
        public string? PendingQuickPlay { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public MainWindow()
        {
            Current = this;
            UiKit.Owner = this;
            InitializeComponent();
            OlChrome.AddHelpButton(this, UpdateManager.OpenGitHubPage);
            OlChrome.Apply(this);
            WebView2Helper.Prepare(NewsWebView);
            DataContext = this;
            StatusText = L("WELCOME", "Welcome");
            GameProfileInfo = L("NO_PROFILE_SELECTED", "No profile selected");
            PlayButton.Content = LocalizedPlay;
            _logTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(120) };
            _logTimer.Tick += (_, _) => FlushLog();
            _logTimer.Start();
            SharingManager.Log += m => AppendLog(m);
            StartupState.FileReceived += path => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Activate();
                OpenFile(path);
            }));
            InstanceManager.Instance.Changed += QueueInstanceRefresh;
            ProfileManager.Instance.Changed += () => UiKit.OnUi(() => { LoadProfiles(); LoadAccountsData(); });
            Loaded += MainWindow_Loaded;
            Loaded += (s, e) => ApplyNewUiLocalization();
        }
        protected override void OnClosed(EventArgs e)
        {
            _discordRpcManager?.Dispose();
            base.OnClosed(e);
        }
        public static void After(int ms, Action a)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (_, _) => { t.Stop(); a(); };
            t.Start();
        }
        private void InitializeDiscordRpc()
        {
            try
            {
                if (DiscordRpcCheckBox.IsChecked == true) StartDiscord();
                else StopDiscord();
            }
            catch (Exception ex)
            {
                LogMessage($"Discord RPC error: {ex.Message}");
            }
        }
        public void StartDiscord()
        {
            try
            {
                if (_discordRpcManager == null)
                {
                    _discordRpcManager = new DiscordRpcManager();
                    _discordRpcManager.Initialize();
                }
                _discordRpcManager.UpdatePresence(L("QT_RPC_IDLE", "Idling in Launcher"));
            }
            catch (Exception ex) { LogMessage($"[Discord] {ex.Message}"); }
        }
        public void StopDiscord()
        {
            try { _discordRpcManager?.Dispose(); } catch { }
            _discordRpcManager = null;
        }
        private void UpdateDiscordRpc(string state, string details = "")
        {
            try
            {
                if (DiscordRpcCheckBox.IsChecked == true) _discordRpcManager?.UpdatePresence(state, details);
            }
            catch { }
        }
        private void RefreshLocalizedStrings()
        {
            try
            {
                CopyLogText.Text = L("COPY_LOG", "Copy Log");
                ExportLogText.Text = L("EXPORT_LOG_MCLOGS", "Export to mclo.gs");
            }
            catch { }
            OnPropertyChanged(string.Empty);
            if (_launch == null) PlayButton.Content = LocalizedPlay;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeApplication();
                var icon = UiKit.FindAsset("images/orange.ico");
                if (icon != null)
                {
                    try { Icon = new BitmapImage(new Uri(icon, UriKind.Absolute)); }
                    catch (Exception ex) { LogMessage($"Failed to load window icon: {ex.Message}"); }
                }
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { WelcomeWizard.ShowIfNeeded(this); }
                    catch (Exception wex) { LogMessage($"Welcome wizard failed: {wex.Message}"); }
                    if (StartupState.PendingMrpackPath is string pending)
                    {
                        StartupState.PendingMrpackPath = null;
                        OpenFile(pending);
                    }
                }), DispatcherPriority.ApplicationIdle);
                After(1500, () => { if (SharingManager.AnyEnabled) Task.Run(SharingManager.ApplyAll); });
                After(3000, DeleteTelemetryIfEnabled);
                After(5000, AutoCheckUpdates);
                _ = VersionService.GetVersionsAsync();
            }
            catch (Exception ex)
            {
                OlMessageBox.Show($"{L("QT_LOAD_ERROR", "Error during window load:")} {ex.Message}", "OrangLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void InitializeApplication()
        {
            try
            {
                LogMessage("Initializing application...");
                LoadProfiles();
                ProfileComboBox.SelectionChanged += ProfileComboBox_SelectionChanged;
                GameProfileComboBox.SelectionChanged += GameProfileComboBox_SelectionChanged;
                MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
                LoadGameProfiles();
                LoadGameProfilesList();
                InitializeSettings();
                await InitializeNewsBrowser();
                ApplyNewsBrowserFromSettings();
                LoadResourcePacks();
                LoadShaderPacks();
                LoadMods();
                LoadServerList();
                if (InstanceManager.Instance.GetInstances().Count > 0) MainTabControl.SelectedIndex = 2;
            }
            catch (Exception ex)
            {
                LogMessage($"Error during initialization: {ex.Message}");
                OlMessageBox.Show($"{L("QT_INIT_ERROR", "Error during initialization:")} {ex.Message}", "OrangLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void QueueInstanceRefresh()
        {
            UiKit.OnUi(() =>
            {
                if (_refreshQueued) return;
                _refreshQueued = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _refreshQueued = false;
                    LoadGameProfilesList();
                    LoadGameProfiles();
                    if (_contentInitialized) RefreshContentInstances();
                }), DispatcherPriority.Background);
            });
        }
        private MinecraftInstance? SelectedInstance() => GameProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is MinecraftInstance instance ? instance : null;
        private string GetCurrentModsPath() => SelectedInstance()?.ModsDir ?? System.IO.Path.Combine(PlatformPaths.GetMinecraftDir(), "mods");
        private string GetCurrentResourcePacksPath(bool isShader)
        {
            var instance = SelectedInstance();
            if (instance != null) return isShader ? instance.ShaderPacksDir : instance.ResourcePacksDir;
            return System.IO.Path.Combine(PlatformPaths.GetMinecraftDir(), isShader ? "shaderpacks" : "resourcepacks");
        }
        private void LoadMods()
        {
            try
            {
                string path = GetCurrentModsPath();
                var mods = ModManager.Instance.GetMods(path);
                _allMods = [.. mods];
                ApplyModFilter();
                ModCountTextBlock.Text = L("QT_MODS_LOADED", "{count} mods loaded", ("count", mods.Count));
                var name = SelectedInstance()?.Name ?? L("NONE", "None");
                ModsProfileInfoTextBlock.Text = $" | {L("QT_INSTANCE", "Instance")}: {name}";
            }
            catch (Exception ex) { LogMessage($"Error loading mods: {ex.Message}"); }
        }
        private void LoadResourcePacks()
        {
            try
            {
                var packs = ResourceManager.Instance.GetPacks(GetCurrentResourcePacksPath(false), false);
                _allResourcePacks = [.. packs];
                ApplyResourcePackFilter();
            }
            catch (Exception ex) { LogMessage($"Error loading resource packs: {ex.Message}"); }
        }
        private void LoadShaderPacks()
        {
            try
            {
                var packs = ResourceManager.Instance.GetPacks(GetCurrentResourcePacksPath(true), true);
                _allShaderPacks = [.. packs];
                ApplyShaderPackFilter();
            }
            catch (Exception ex) { LogMessage($"Error loading shader packs: {ex.Message}"); }
        }
        private void ApplyModFilter()
        {
            var filter = ModsSearchTextBox.Text;
            ModsListBox.ItemsSource = new ObservableCollection<ModInfo>(string.IsNullOrWhiteSpace(filter) ? _allMods : _allMods.Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ApplyResourcePackFilter()
        {
            var filter = ResourcePacksSearchTextBox.Text;
            ResourcePacksListBox.ItemsSource = new ObservableCollection<ResourcePackInfo>(string.IsNullOrWhiteSpace(filter) ? _allResourcePacks : _allResourcePacks.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ApplyShaderPackFilter()
        {
            var filter = ShaderPacksSearchTextBox.Text;
            ShaderPacksListBox.ItemsSource = new ObservableCollection<ResourcePackInfo>(string.IsNullOrWhiteSpace(filter) ? _allShaderPacks : _allShaderPacks.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ModsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyModFilter();
        private void ResourcePacksSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyResourcePackFilter();
        private void ShaderPacksSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyShaderPackFilter();
        private string[]? PickContentFiles(string title, string typeName, string pattern)
            => UiKit.PickFiles(title, UiKit.Filter((typeName, pattern)) + "|" + UiKit.AllFiles);
        private void AddModButton_Click(object sender, RoutedEventArgs e)
        {
            var files = PickContentFiles(L("MODS_FILE_SELECT_TITLE", "Select mods"), L("QT_FT_MODS", "Mod files"), "*.jar");
            if (files == null) return;
            try { ModManager.Instance.AddMods(GetCurrentModsPath(), files); }
            catch (Exception ex) { OlMessageBox.Show(this, ex.Message, LocalizedAddMod, MessageBoxButton.OK, MessageBoxImage.Error); }
            LoadMods();
        }
        private void RemoveModButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModsListBox.SelectedItems.Cast<ModInfo>().ToList();
            if (selectedMods.Count > 0) { ModManager.Instance.RemoveMods(selectedMods); LoadMods(); }
        }
        private void OpenModsFolderButton_Click(object sender, RoutedEventArgs e) => ModManager.Instance.OpenModsFolder(GetCurrentModsPath());
        private void RefreshModsButton_Click(object sender, RoutedEventArgs e) => LoadMods();
        private void AddResourcePackButton_Click(object sender, RoutedEventArgs e)
        {
            var files = PickContentFiles(L("QT_SELECT_PACKS", "Select packs"), L("QT_FT_PACKS", "Packs"), "*.zip");
            if (files == null) return;
            try { ResourceManager.Instance.AddPacks(GetCurrentResourcePacksPath(false), files); }
            catch (Exception ex) { OlMessageBox.Show(this, ex.Message, LocalizedAddPack, MessageBoxButton.OK, MessageBoxImage.Error); }
            LoadResourcePacks();
        }
        private void RefreshResourcePacksButton_Click(object sender, RoutedEventArgs e) => LoadResourcePacks();
        private void RemoveResourcePackButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPacks = ResourcePacksListBox.SelectedItems.Cast<ResourcePackInfo>().ToList();
            if (selectedPacks.Count > 0) { ResourceManager.Instance.RemovePacks(selectedPacks); LoadResourcePacks(); }
        }
        private void OpenResourcePacksButton_Click(object sender, RoutedEventArgs e) => ResourceManager.Instance.OpenFolder(GetCurrentResourcePacksPath(false));
        private void AddShaderPackButton_Click(object sender, RoutedEventArgs e)
        {
            var files = PickContentFiles(L("QT_SELECT_PACKS", "Select packs"), L("QT_FT_PACKS", "Packs"), "*.zip");
            if (files == null) return;
            try { ResourceManager.Instance.AddPacks(GetCurrentResourcePacksPath(true), files); }
            catch (Exception ex) { OlMessageBox.Show(this, ex.Message, LocalizedAddShader, MessageBoxButton.OK, MessageBoxImage.Error); }
            LoadShaderPacks();
        }
        private void RefreshShaderPacksButton_Click(object sender, RoutedEventArgs e) => LoadShaderPacks();
        private void RemoveShaderPackButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPacks = ShaderPacksListBox.SelectedItems.Cast<ResourcePackInfo>().ToList();
            if (selectedPacks.Count > 0) { ResourceManager.Instance.RemovePacks(selectedPacks); LoadShaderPacks(); }
        }
        private void OpenShaderPacksButton_Click(object sender, RoutedEventArgs e) => ResourceManager.Instance.OpenFolder(GetCurrentResourcePacksPath(true));
        private void RefreshServerButton_Click(object sender, RoutedEventArgs e) => LoadServerList();
        private void LoadServerList()
        {
            try
            {
                ServersListBox.ItemsSource = null;
                var instance = SelectedInstance();
                if (instance == null) return;
                var path = instance.MinecraftDir;
                if (!Directory.Exists(path)) return;
                var servers = ServerListManager.Instance.LoadServers(path);
                var viewModels = new ObservableCollection<ServerViewModel>();
                foreach (var server in servers) viewModels.Add(new ServerViewModel(server) { SupportsQuickPlay = true });
                ServersListBox.ItemsSource = viewModels;
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading servers: {ex.Message}");
            }
        }
        private void QuickPlayServer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ServerViewModel viewModel)
            {
                PendingQuickPlay = viewModel.Server.Ip;
                LogMessage($"Quick Play: {viewModel.Server.Name} ({viewModel.Server.Ip})");
                LaunchGame();
            }
        }
        private void AddServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var instance = SelectedInstance();
                if (instance == null)
                {
                    OlMessageBox.Show(L("QT_SELECT_INSTANCE_FIRST", "Select an instance first."), LocalizedServerManager, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var nameDlg = new AddTextDialog(L("QT_SERVER_NAME", "Server name:"), LocalizedAddServer, "Minecraft Server") { Owner = this };
                if (nameDlg.ShowDialog() != true || string.IsNullOrWhiteSpace(nameDlg.ResultText)) return;
                var ipDlg = new AddTextDialog(L("QT_SERVER_ADDRESS", "Server address:"), LocalizedAddServer, "") { Owner = this };
                if (ipDlg.ShowDialog() != true || string.IsNullOrWhiteSpace(ipDlg.ResultText)) return;
                var path = instance.MinecraftDir;
                Directory.CreateDirectory(path);
                var servers = ServerListManager.Instance.LoadServers(path);
                servers.Add(new ServerInfo { Name = nameDlg.ResultText.Trim(), Ip = ipDlg.ResultText.Trim() });
                ServerListManager.Instance.SaveServers(path, servers);
                LogMessage($"Added server '{nameDlg.ResultText}' ({ipDlg.ResultText}) to {path}");
                LoadServerList();
            }
            catch (Exception ex)
            {
                OlMessageBox.Show(ex.Message, LocalizedServerManager, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RemoveServerButton_Click(object sender, RoutedEventArgs e)
        {
            var instance = SelectedInstance();
            if (ServersListBox.SelectedItem is not ServerViewModel viewModel || instance == null) return;
            var server = viewModel.Server;
            var servers = ServerListManager.Instance.LoadServers(instance.MinecraftDir);
            var toRemove = servers.FirstOrDefault(s => s.Name == server.Name && s.Ip == server.Ip);
            if (toRemove == null) return;
            servers.Remove(toRemove);
            ServerListManager.Instance.SaveServers(instance.MinecraftDir, servers);
            LoadServerList();
        }
        private void ManageAccountsButton_Click(object? sender, RoutedEventArgs? e)
        {
            MainTabControl.SelectedItem = SettingsTabItem;
            ShowSettingsPanel("Accounts");
        }
        public void ShowPage(string page)
        {
            switch (page)
            {
                case "logs": MainTabControl.SelectedIndex = 1; break;
                case "instances": MainTabControl.SelectedIndex = 2; break;
                case "content": MainTabControl.SelectedItem = ContentTabItem; break;
                case "settings": MainTabControl.SelectedItem = SettingsTabItem; break;
                default: MainTabControl.SelectedIndex = 0; break;
            }
        }
        public void ShowContent(string type)
        {
            MainTabControl.SelectedItem = ContentTabItem;
            InitContentPage();
            var inst = _editor?.Instance ?? SelectedInstance();
            if (inst != null)
            {
                var idx = _contentInstances.FindIndex(i => i.InstanceId == inst.InstanceId);
                if (idx >= 0)
                {
                    _suppressContentSearch = true;
                    ContentInstanceComboBox.SelectedIndex = idx + 1;
                    _suppressContentSearch = false;
                }
            }
            _contentType = type;
            _contentOffset = 0;
            _ = RunContentSearchAsync();
        }
        private void InstanceContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
        private void InstanceMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;
            var headers = new[] { LocalizedInstanceSelect, LocalizedInstanceEdit, LocalizedInstanceDuplicate, LocalizedInstanceOpenFolder, LocalizedInstanceExport, LocalizedInstanceDelete };
            int i = 0;
            foreach (var item in menu.Items)
                if (item is MenuItem mi && i < headers.Length) mi.Header = headers[i++];
        }
        private void SelectInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance) SelectInstanceById(instance.InstanceId);
        }
        private void SelectInstanceById(string id)
        {
            InstanceManager.Instance.SetSelectedInstance(id);
            LoadGameProfiles();
        }
        private void OpenInstanceFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance)
            {
                Directory.CreateDirectory(instance.MinecraftDir);
                UiKit.OpenPath(instance.MinecraftDir);
            }
        }
        private void ExportInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance) OpenEditor(instance, "modpack");
        }
        private void DeleteInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance) DeleteInstance(instance);
        }
        public bool DeleteInstance(MinecraftInstance instance)
        {
            var msg = L("GAME_PROFILES_DELETE_CONFIRM_MSG", "Delete '{name}' and all of its files?", ("name", instance.Name));
            if (OlMessageBox.Show(this, msg, L("GAME_PROFILES_DELETE_CONFIRM_TITLE", "Delete instance"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return false;
            if (!InstanceManager.Instance.RemoveInstance(instance.InstanceId)) return false;
            LogMessage($"Profile deleted: {instance.Name}");
            LoadGameProfilesList();
            LoadGameProfiles();
            return true;
        }
        private void DuplicateInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance) DuplicateInstance(instance);
        }
        public async void DuplicateInstance(MinecraftInstance instance)
        {
            SetBusy(true, L("QT_COPYING_FILES", "Copying files..."));
            try
            {
                var copy = await Task.Run(() => InstanceManager.Instance.Duplicate(instance));
                LogMessage($"Duplicated profile to '{copy.Name}'");
                SetBusy(false, L("QT_READY", "Ready"));
                LoadGameProfilesList();
                LoadGameProfiles();
            }
            catch (Exception ex)
            {
                SetBusy(false, L("QT_READY", "Ready"));
                OlMessageBox.Show(this, ex.Message, LocalizedDuplicate, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void InstanceIconButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = L("QT_CHOOSE_ICON", "Choose icon"),
                Filter = $"{L("QT_FT_IMAGES", "Images")} (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|{UiKit.AllFiles}"
            };
            if (dialog.ShowDialog(this) != true) return;
            _tempIconPath = dialog.FileName;
            InstanceIconImage.Source = UiKit.LoadImage(_tempIconPath, 128);
        }
        private void UpdateEditViewIcon()
        {
            if (!string.IsNullOrEmpty(_tempIconPath) && File.Exists(_tempIconPath))
            {
                InstanceIconImage.Source = UiKit.LoadImage(_tempIconPath, 128);
                return;
            }
            var loader = (ProfileModLoaderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "vanilla";
            InstanceIconImage.Source = InstanceIcons.For(new MinecraftInstance { Name = ProfileNameTextBox.Text, ModLoader = loader }, 64);
        }
        private void SelectAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserProfile profile)
            {
                ProfileManager.Instance.SetSelectedProfile(profile.Uuid);
                LoadProfiles();
                StatusText = L("QT_SWITCHED_ACCOUNT", "Switched to account: {name}", ("name", profile.Username));
            }
        }
        private void RemoveAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserProfile profile)
            {
                if (OlMessageBox.Show(this, L("QT_REMOVE_ACCOUNT_Q", "Remove account '{name}'?", ("name", profile.Username)), LocalizedRemove, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    ProfileManager.Instance.RemoveProfile(profile.Uuid);
            }
        }
        private void LoadProfiles()
        {
            _filling = true;
            try
            {
                ProfileComboBox.Items.Clear();
                var profiles = ProfileManager.Instance.GetProfiles();
                foreach (var profile in profiles) ProfileComboBox.Items.Add(new ComboBoxItem { Content = profile.GetDisplayName(), Tag = profile });
                var selected = ProfileManager.Instance.GetSelectedProfile();
                for (int i = 0; i < ProfileComboBox.Items.Count; i++)
                {
                    if (ProfileComboBox.Items[i] is ComboBoxItem item && item.Tag is UserProfile p && p.Uuid == selected?.Uuid)
                    {
                        ProfileComboBox.SelectedIndex = i;
                        break;
                    }
                }
                if (ProfileComboBox.SelectedIndex < 0 && ProfileComboBox.Items.Count > 0) ProfileComboBox.SelectedIndex = 0;
            }
            catch (Exception ex) { LogMessage($"Error loading profiles: {ex.Message}"); }
            finally { _filling = false; }
        }
        private void LoadGameProfiles()
        {
            _filling = true;
            try
            {
                GameProfileComboBox.Items.Clear();
                var instances = InstanceManager.Instance.GetInstances();
                foreach (var instance in instances) GameProfileComboBox.Items.Add(new ComboBoxItem { Content = instance.Name, Tag = instance });
                var selected = InstanceManager.Instance.GetSelectedInstance();
                for (int i = 0; i < GameProfileComboBox.Items.Count; i++)
                {
                    if (GameProfileComboBox.Items[i] is ComboBoxItem item && item.Tag is MinecraftInstance inst && inst.InstanceId == selected?.InstanceId)
                    {
                        GameProfileComboBox.SelectedIndex = i;
                        break;
                    }
                }
                if (GameProfileComboBox.SelectedIndex < 0 && GameProfileComboBox.Items.Count > 0) GameProfileComboBox.SelectedIndex = 0;
            }
            catch (Exception ex) { LogMessage($"Error: {ex.Message}"); }
            finally { _filling = false; }
            UpdateGameProfileInfo();
        }
        private void UpdateGameProfileInfo()
        {
            var instance = SelectedInstance();
            if (instance != null)
            {
                GameProfileInfo = $"{L("QT_INSTANCE", "Instance")}: {instance.Name} | {instance.Version} ({instance.LoaderKey})";
                UpdateModdingTabState(instance.LoaderKey);
            }
            else
            {
                GameProfileInfo = L("NO_PROFILE_SELECTED", "No profile selected");
                UpdateModdingTabState("vanilla");
            }
        }
        private void UpdateModdingTabState(string? modLoader)
        {
            bool shouldDisable = string.IsNullOrEmpty(modLoader) || modLoader is "vanilla" or "none";
            ModdingTabItem.IsEnabled = !shouldDisable;
            ModdingTabItem.Opacity = shouldDisable ? 0.5 : 1.0;
            if (shouldDisable && ResourcePacksTabControl.SelectedItem == ModdingTabItem) ResourcePacksTabControl.SelectedIndex = 0;
        }
        private void SetPlayMode(string mode)
        {
            _playMode = mode;
            UiKit.OnUi(() =>
            {
                PlayButton.Content = mode switch
                {
                    "play" => LocalizedPlay,
                    "stop" => LocalizedStop,
                    _ => L("CANCELLING", "Cancelling...")
                };
                PlayButton.IsEnabled = mode != "cancelling";
            });
        }
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playMode == "play") LaunchGame();
            else CancelLaunch();
        }
        public async void LaunchGame()
        {
            if (_launch != null)
            {
                OlMessageBox.Show(this, L("LAUNCHER_BUSY_MSG", "A launch is already in progress."), L("LAUNCHER_BUSY", "Busy"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var account = ProfileManager.Instance.GetSelectedProfile();
            if (account == null)
            {
                PendingQuickPlay = null;
                ManageAccountsButton_Click(null, null);
                OlMessageBox.Show(this, L("QT_ACCOUNT_REQUIRED_MSG", "Please add or select an account before playing."), L("QT_NO_ACCOUNT_TITLE", "No account added"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var instance = InstanceManager.Instance.GetSelectedInstance();
            if (instance == null)
            {
                PendingQuickPlay = null;
                MainTabControl.SelectedIndex = 2;
                OlMessageBox.Show(this, L("QT_SELECT_INSTANCE_FIRST", "Select an instance first."), L("NO_PROFILE_SELECTED", "No profile selected"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(instance.Version))
            {
                PendingQuickPlay = null;
                OlMessageBox.Show(this, L("QT_VERSION_ERROR_MSG", "Selected instance has no Minecraft version set. Edit the instance to set a valid version."), L("QT_VERSION_ERROR", "Version error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var quickPlay = PendingQuickPlay;
            PendingQuickPlay = null;
            var launch = new LaunchService();
            _launch = launch;
            launch.Log += AppendLog;
            launch.Progress += (p, t) => SetProgress(p, t);
            launch.GameStarted += OnGameStarted;
            SetPlayMode("stop");
            ProfileComboBox.IsEnabled = false;
            GameProfileComboBox.IsEnabled = false;
            StatusText = L("QT_LAUNCHING", "Launching {version} ({loader})...", ("version", instance.Version), ("loader", instance.LoaderKey));
            UpdateDiscordRpc(L("QT_RPC_PLAYING", "Playing Minecraft"), $"{instance.Version} ({instance.LoaderKey})");
            try
            {
                var result = await Task.Run(() => launch.RunAsync(new LaunchRequest
                {
                    Instance = instance,
                    Account = account,
                    QuickPlayServer = quickPlay,
                    GlobalDiscreteGpu = LauncherSettings.UseDiscreteGpu
                }));
                StatusText = L("QT_GAME_EXITED", "Game exited ({code})", ("code", result.ExitCode));
                if (result.Started && result.ExitCode != 0 && result.CrashReportPath != null)
                    new CrashDialog(result.ExitCode, result.CrashReportPath, result.CrashSummary ?? "").Show();
            }
            catch (OperationCanceledException)
            {
                AppendLog("[Launcher] Launch cancelled");
                StatusText = L("QT_STOPPED", "Stopped");
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Launch failed: {ex.Message}");
                AppendLog(ex.ToString());
                StatusText = L("QT_LAUNCH_FAILED", "Launch failed");
                var message = ex.Message;
                if (ex.Message.Contains("does not exist"))
                {
                    var hint = VersionService.Suggestions(instance.Version);
                    if (hint.Count > 0) message += "\n\n" + L("QT_DID_YOU_MEAN", "Did you mean: {options}?", ("options", string.Join(", ", hint)));
                }
                OlMessageBox.Show(this, message, L("QT_LAUNCH_FAILED", "Launch failed"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _launch = null;
                SetPlayMode("play");
                ProfileComboBox.IsEnabled = true;
                GameProfileComboBox.IsEnabled = true;
                UpdateDiscordRpc(L("QT_RPC_IDLE", "Idling in Launcher"));
                LoadGameProfilesList();
            }
        }
        private void OnGameStarted()
        {
            UiKit.OnUi(() =>
            {
                var inst = InstanceManager.Instance.GetSelectedInstance();
                var acc = ProfileManager.Instance.GetSelectedProfile();
                if (inst != null)
                    StatusText = L("QT_RUNNING_AS", "Minecraft {version} ({loader}) running as {username}", ("version", inst.Version), ("loader", inst.LoaderKey), ("username", acc?.Username ?? ""));
                SetPlayMode("stop");
            });
        }
        private void CancelLaunch()
        {
            if (_launch == null) return;
            if (_launch.IsGameRunning)
            {
                _launch.Stop();
                StatusText = L("STOPPED", "Stopped");
            }
            else
            {
                SetPlayMode("cancelling");
                StatusText = L("CANCELLING_LAUNCH", "Cancelling launch...");
                _launch.Cancel();
            }
        }
        public void SetStatus(string text) => UiKit.OnUi(() => StatusText = text);
        public void SetProgress(int percent, string? text = null)
        {
            UiKit.OnUi(() =>
            {
                if (!string.IsNullOrEmpty(text)) StatusText = text;
            });
        }
        public void SetBusy(bool busy, string? text = null)
        {
            UiKit.OnUi(() =>
            {
                if (text != null) StatusText = text;
                Cursor = busy ? System.Windows.Input.Cursors.AppStarting : null;
            });
        }
        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            lock (_pendingLog) _pendingLog.Clear();
            LogTextBox.Clear();
        }
        private void CopyLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FlushLog();
                Clipboard.SetText(LogTextBox.Text ?? "");
                StatusText = L("LOG_COPIED", "Log copied to clipboard.");
            }
            catch (Exception ex) { LogMessage($"Copy log failed: {ex.Message}"); }
        }
        private async void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FlushLog();
                ExportLogButton.IsEnabled = false;
                StatusText = L("LOG_UPLOADING", "Uploading log to mclo.gs...");
                var (success, urlOrError) = await MclogsClient.UploadAsync(LogTextBox.Text ?? "");
                if (success)
                {
                    try { Clipboard.SetText(urlOrError); } catch { }
                    StatusText = $"{L("LOG_UPLOADED", "Log uploaded (link copied):")} {urlOrError}";
                    UiKit.OpenUrl(urlOrError);
                }
                else
                {
                    StatusText = $"{L("LOG_UPLOAD_FAILED", "Log upload failed:")} {urlOrError}";
                }
            }
            catch (Exception ex) { LogMessage($"Export log failed: {ex.Message}"); }
            finally { ExportLogButton.IsEnabled = true; }
        }
        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filling) return;
            if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile profile)
            {
                ProfileManager.Instance.SetSelectedProfile(profile.Uuid);
                StatusText = L("WELCOME_USER", "Welcome, {username}", ("username", profile.Username));
            }
        }
        private void ProfileVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileModLoaderComboBox.SelectedItem == null) ProfileModLoaderComboBox.SelectedIndex = 0;
            else ProfileModLoaderComboBox_SelectionChanged(ProfileModLoaderComboBox, e);
        }
        private int _loaderFetch;
        private async void ProfileModLoaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileModLoaderComboBox.SelectedItem is not ComboBoxItem selected) return;
            var loaderType = selected.Tag?.ToString()?.ToLowerInvariant() ?? "vanilla";
            UpdateEditViewIcon();
            var mcVersion = ProfileVersionComboBox.SelectedItem?.ToString() ?? ProfileVersionComboBox.Text;
            if (loaderType == "vanilla")
            {
                ProfileModLoaderVersionComboBox.ItemsSource = null;
                ProfileModLoaderVersionComboBox.Text = "";
                ProfileModLoaderVersionComboBox.IsEnabled = false;
                return;
            }
            ProfileModLoaderVersionComboBox.IsEnabled = true;
            var latest = L("QT_LATEST", "Latest");
            if (string.IsNullOrEmpty(mcVersion))
            {
                ProfileModLoaderVersionComboBox.ItemsSource = new[] { latest };
                ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                return;
            }
            var token = ++_loaderFetch;
            ProfileModLoaderVersionComboBox.ItemsSource = new[] { L("QT_LOADING", "Loading...") };
            ProfileModLoaderVersionComboBox.SelectedIndex = 0;
            try
            {
                var versions = await LoaderVersionService.FetchAsync(loaderType, mcVersion);
                if (token != _loaderFetch) return;
                if (versions.Count > 0)
                {
                    ProfileModLoaderVersionComboBox.ItemsSource = new List<string> { latest }.Concat(versions).ToList();
                    ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                }
                else
                {
                    ProfileModLoaderVersionComboBox.ItemsSource = new[] { L("QT_NOT_AVAILABLE_FOR_VERSION", "Not available for this MC version") };
                    ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                if (token != _loaderFetch) return;
                LogMessage($"Error fetching loader versions: {ex.Message}");
                ProfileModLoaderVersionComboBox.ItemsSource = new[] { latest };
                ProfileModLoaderVersionComboBox.SelectedIndex = 0;
            }
        }
        private void GameProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filling) return;
            var instance = SelectedInstance();
            if (instance != null) InstanceManager.Instance.SetSelectedInstance(instance.InstanceId);
            UpdateGameProfileInfo();
            LoadResourcePacks();
            LoadShaderPacks();
            LoadMods();
            LoadServerList();
        }
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl || MainTabControl.SelectedItem is not TabItem selectedTab) return;
            var index = MainTabControl.SelectedIndex;
            if (index == 1) { FlushLog(); Dispatcher.BeginInvoke(new Action(() => LogTextBox.ScrollToEnd()), DispatcherPriority.Background); }
            else if (index == 3) { LoadResourcePacks(); LoadShaderPacks(); LoadMods(); }
            else if (index == 4) LoadServerList();
            else if (ReferenceEquals(selectedTab, ContentTabItem)) InitContentPage();
            else if (ReferenceEquals(selectedTab, SettingsTabItem) && AccountsSettingsPanel.Visibility == Visibility.Visible) _ = LoadCurrentAccountSkinAsync();
        }
        private void ResourcePacksTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is not TabControl) return;
            switch (ResourcePacksTabControl.SelectedIndex)
            {
                case 0: LoadResourcePacks(); break;
                case 1: LoadShaderPacks(); break;
                case 2: LoadMods(); break;
            }
        }
        private void GetCurrentInstanceVersionInfo(out string version, out string loader)
        {
            var instance = SelectedInstance();
            version = instance?.Version ?? "";
            loader = instance?.LoaderKey ?? "vanilla";
            if (string.IsNullOrWhiteSpace(version))
            {
                var detected = InstalledVersionDetector.Detect(PlatformPaths.GetMinecraftDir());
                version = detected.GameVersion ?? "";
                loader = detected.Loader ?? "vanilla";
            }
            loader = loader.ToLowerInvariant();
        }
        private void UpdateResourcePacksButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out _);
            new UpdateWindow(GetCurrentResourcePacksPath(false), "resourcepack", version) { Owner = this }.ShowDialog();
            LoadResourcePacks();
        }
        private void UpdateShaderPacksButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out _);
            new UpdateWindow(GetCurrentResourcePacksPath(true), "shader", version) { Owner = this }.ShowDialog();
            LoadShaderPacks();
        }
        private void UpdateModsButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out string loader);
            if (loader is "vanilla" or "none")
            {
                OlMessageBox.Show(this, L("QT_UPDATE_NEEDS_LOADER", "Please select a mod loader (Fabric/Forge) in your profile settings before updating mods."), LocalizedUpdate, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            new UpdateWindow(GetCurrentModsPath(), "mod", version, loader) { Owner = this }.ShowDialog();
            LoadMods();
        }
        public void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            LogMessage(message.TrimEnd());
        }
        private void LogMessage(string message)
        {
            var line = Regex.IsMatch(message, @"^\[\d{2}:\d{2}:\d{2}") ? message : $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (LauncherSettings.DebugModeEnabled)
            {
                lock (_debugLock)
                {
                    try { File.AppendAllText(_debugLog, line + Environment.NewLine); } catch { }
                }
            }
            lock (_pendingLog) _pendingLog.Add(line);
        }
        private void FlushLog()
        {
            string[] lines;
            lock (_pendingLog)
            {
                if (_pendingLog.Count == 0) return;
                lines = _pendingLog.ToArray();
                _pendingLog.Clear();
            }
            if (LogTextBox.Text.Length > 400_000)
                LogTextBox.Text = "[...]\n" + LogTextBox.Text[^300_000..];
            LogTextBox.AppendText(string.Join("\n", lines) + "\n");
            LogTextBox.ScrollToEnd();
        }
        private void AddProfileButton_Click(object sender, RoutedEventArgs e)
        {
            _tempIconPath = null;
            ProfileNameTextBox.Text = InstanceManager.Instance.UniqueName(L("QT_NEW_INSTANCE_NAME", "New Instance"));
            ProfileModLoaderComboBox.SelectedIndex = 0;
            ProfileRamSlider.Value = 4096;
            ProfileJavaArgsTextBox.Text = "";
            ProfilePerformanceTierComboBox.SelectedIndex = 0;
            UpdateEditViewIcon();
            ShowEditView();
        }
        private void EditInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance) OpenEditor(instance);
        }
        public void OpenEditor(MinecraftInstance instance, string? tab = null)
        {
            MainTabControl.SelectedIndex = 2;
            if (_editor == null)
            {
                _editor = new InstanceEditor(this, CloseEditor);
                GameProfilesEditorHost.Children.Add(_editor);
            }
            GameProfilesListView.Visibility = Visibility.Collapsed;
            GameProfilesEditView.Visibility = Visibility.Collapsed;
            GameProfilesEditorHost.Visibility = Visibility.Visible;
            _editor.Open(instance, tab);
        }
        private void CloseEditor()
        {
            GameProfilesEditorHost.Visibility = Visibility.Collapsed;
            GameProfilesListView.Visibility = Visibility.Visible;
            LoadGameProfilesList();
            LoadGameProfiles();
        }
        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = ProfileNameTextBox.Text.Trim();
                string version = (ProfileVersionComboBox.SelectedItem?.ToString() ?? ProfileVersionComboBox.Text).Trim();
                string modLoader = (ProfileModLoaderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.ToLowerInvariant() ?? "vanilla";
                string loaderVersion = ProfileModLoaderVersionComboBox.SelectedItem?.ToString() ?? ProfileModLoaderVersionComboBox.Text ?? "";
                if (!LoaderVersionLooksReal(loaderVersion)) loaderVersion = "";
                string ram = ((int)ProfileRamSlider.Value).ToString();
                string javaArgs = ProfileJavaArgsTextBox.Text.Trim();
                string performanceTier = (ProfilePerformanceTierComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";
                string? javaPath = (ProfileJavaComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
                if (string.IsNullOrEmpty(name))
                {
                    OlMessageBox.Show(this, L("GAME_PROFILES_NAME_EMPTY", "Please enter a name."), LocalizedCreateProfile, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(version))
                {
                    OlMessageBox.Show(this, L("QT_VERSION_ERROR_MSG", "Selected instance has no Minecraft version set. Edit the instance to set a valid version."), LocalizedCreateProfile, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (InstanceManager.Instance.GetInstanceByName(name) != null) name = InstanceManager.Instance.UniqueName(name);
                var instance = InstanceManager.Instance.CreateInstance(name, version, modLoader, ram, string.IsNullOrEmpty(loaderVersion) ? null : loaderVersion);
                if (instance == null) return;
                instance.JavaArgs = javaArgs;
                instance.Options.JvmArgs = javaArgs;
                instance.JavaPath = javaPath;
                instance.PerformanceTier = performanceTier;
                if (!string.IsNullOrEmpty(_tempIconPath)) InstanceManager.Instance.SetIcon(instance, _tempIconPath);
                InstanceManager.Instance.SetSelectedInstance(instance.InstanceId);
                InstanceManager.Instance.SaveInstances();
                try { SharingManager.Apply(instance); } catch { }
                LogMessage($"Created new instance: {name} ({version}, {modLoader})");
                HideEditView();
                LoadGameProfilesList();
                LoadGameProfiles();
            }
            catch (Exception ex)
            {
                OlMessageBox.Show(this, ex.Message, LocalizedCreateProfile, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static bool LoaderVersionLooksReal(string text)
            => !string.IsNullOrWhiteSpace(text) && text.Any(char.IsDigit) && !text.Contains(' ');
        private void ImportModpackButton_Click(object sender, RoutedEventArgs e)
            => PickAndImport(L("QT_IMPORT_KIND_MRPACK", "Modrinth modpack"), "*.mrpack;*.zip");
        private void ImportOrangpackButton_Click(object sender, RoutedEventArgs e)
            => PickAndImport(L("QT_IMPORT_KIND_ORANGPACK", "OrangLauncher pack"), "*.orangpack");
        private void PickAndImport(string title, string pattern)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = $"{title} ({pattern.Replace(";", ", ")})|{pattern}|{UiKit.AllFiles}"
            };
            if (dialog.ShowDialog(this) == true) OpenFile(dialog.FileName);
        }
        public void OpenFile(string path)
        {
            var low = path.ToLowerInvariant();
            if (low.EndsWith(".mrpack") || low.EndsWith(ModpackImporter.OrangpackExt))
            {
                ImportPack(path);
                return;
            }
            if (!low.EndsWith(".zip")) return;
            try
            {
                using var zip = System.IO.Compression.ZipFile.OpenRead(path);
                var names = zip.Entries.Select(x => x.FullName).ToHashSet();
                if (!names.Contains("instance.json") && (names.Contains("modrinth.index.json") || names.Contains(ModpackImporter.OrangpackIndex)))
                {
                    ImportPack(path);
                    return;
                }
                if (!names.Contains("instance.json") && names.Contains("manifest.json"))
                {
                    ImportCurseForge(path);
                    return;
                }
            }
            catch { }
            ImportInstanceZip(path);
        }
        public async void ImportPack(string path, bool showResult = true)
        {
            SetBusy(true, L("QT_IMPORTING_MODPACK", "Importing modpack..."));
            var importer = new ModpackImporter();
            importer.ProgressChanged += (t, p) => SetStatus($"{t} ({p:F0}%)");
            importer.Log += AppendLog;
            var (ok, message, id) = await Task.Run(() => importer.ImportMrPackAsync(path));
            SetBusy(false, message);
            InstanceManager.Instance.SaveInstances();
            if (ok && id != null) SelectInstanceById(id);
            if (ok && showResult) OlMessageBox.Show(this, message, L("QT_PACK_IMPORTED", "Pack imported"), MessageBoxButton.OK, MessageBoxImage.Information);
            else if (!ok) OlMessageBox.Show(this, message, L("QT_IMPORT_FAILED", "Import failed"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        public async void ImportCurseForge(string path)
        {
            SetBusy(true, L("QT_IMPORTING_CURSEFORGE", "Importing CurseForge pack..."));
            var importer = new ModpackImporter();
            importer.Log += AppendLog;
            var (ok, message, id) = await Task.Run(() => importer.ImportCurseForgeAsync(path));
            SetBusy(false, message);
            if (ok && id != null) SelectInstanceById(id);
            OlMessageBox.Show(this, message, ok ? L("QT_PACK_IMPORTED", "Pack imported") : L("QT_IMPORT_FAILED", "Import failed"), MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        private async void ImportInstanceZip(string path)
        {
            SetBusy(true, L("QT_IMPORTING_INSTANCE", "Importing instance..."));
            try
            {
                var inst = await Task.Run(() => InstanceManager.Instance.ImportInstanceZip(path));
                try { SharingManager.Apply(inst); } catch { }
                SetBusy(false, L("QT_READY", "Ready"));
                SelectInstanceById(inst.InstanceId);
                OlMessageBox.Show(this, L("QT_IMPORTED_AS", "Imported: {name}", ("name", inst.Name)), L("QT_IMPORT", "Import"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetBusy(false, L("QT_READY", "Ready"));
                OlMessageBox.Show(this, ex.Message, L("QT_IMPORT_FAILED", "Import failed"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CancelProfileButton_Click(object sender, RoutedEventArgs e) => HideEditView();
        private void ShowEditView()
        {
            GameProfilesListView.Visibility = Visibility.Collapsed;
            GameProfilesEditorHost.Visibility = Visibility.Collapsed;
            GameProfilesEditView.Visibility = Visibility.Visible;
            ConfigureRamSlider();
            RefreshJavaComboBoxInProfile();
            LoadVersionsAsync();
        }
        private void ConfigureRamSlider()
        {
            try
            {
                int maxMb = SystemPerformanceHelper.GetMaxGameRamMB();
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
        }
        private void LoadGameProfilesList()
        {
            try
            {
                var instances = InstanceManager.Instance.GetInstances();
                _gameInstances.Clear();
                foreach (var inst in instances) _gameInstances.Add(inst);
            }
            catch (Exception ex) { LogMessage($"Error list: {ex.Message}"); }
        }
        private async void LoadVersionsAsync()
        {
            try
            {
                var keep = ProfileVersionComboBox.SelectedItem as string;
                var versions = await VersionService.GetVersionsAsync();
                var types = LauncherSettings.VisibleVersionTypes();
                var versionIds = versions.Where(v => types.Contains(v.Type)).Select(v => v.Id).Distinct().ToList();
                if (versionIds.Count == 0) throw new InvalidOperationException();
                ProfileVersionComboBox.ItemsSource = versionIds;
                var idx = keep == null ? -1 : versionIds.IndexOf(keep);
                ProfileVersionComboBox.SelectedIndex = idx >= 0 ? idx : 0;
            }
            catch
            {
                ProfileVersionComboBox.ItemsSource = new[] { "1.21.1", "1.20.4", "1.20.1", "1.19.4", "1.18.2", "1.16.5", "1.12.2", "1.8.9", "1.7.10" };
                ProfileVersionComboBox.SelectedIndex = 0;
            }
        }
        private void InitializeSettings()
        {
            try
            {
                LanguageComboBox.Items.Clear();
                foreach (var langCode in LocalizationManager.GetAvailableLanguages())
                    LanguageComboBox.Items.Add(new ComboBoxItem { Content = LocalizationManager.GetLanguageDisplayName(langCode), Tag = langCode });
                GeneralSettingsButton.Click += (s, e) => ShowSettingsPanel("General");
                AccountsSettingsButton.Click += (s, e) => ShowSettingsPanel("Accounts");
                AdvancedSettingsButton.Click += (s, e) => ShowSettingsPanel("Advanced");
                AboutSettingsButton.Click += (s, e) => ShowSettingsPanel("About");
                LoadSettingsData();
                LoadAccountsData();
                LoadPluginsData();
                LoadEnabledPlugins();
                LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
                ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
                AddMicrosoftAccountButton.Click += AddMicrosoftAccountButton_Click;
                AddOfflineAccountButton.Click += AddOfflineAccountButton_Click;
                DiscordRpcCheckBox.Checked += (s, e) => { SaveSettings(); InitializeDiscordRpc(); };
                DiscordRpcCheckBox.Unchecked += (s, e) => { SaveSettings(); InitializeDiscordRpc(); };
                DeleteTelemetryCheckBox.Checked += (s, e) => SaveSettings();
                DeleteTelemetryCheckBox.Unchecked += (s, e) => SaveSettings();
                ModernTitleBarCheckBox.Checked += (s, e) => ModernTitleBarChanged();
                ModernTitleBarCheckBox.Unchecked += (s, e) => ModernTitleBarChanged();
                DebugModeCheckBox.Checked += DebugModeCheckBox_Checked;
                DebugModeCheckBox.Unchecked += DebugModeCheckBox_Unchecked;
                SuppressGameBarCheckBox.IsChecked = GameBarFix.Enabled;
                SuppressGameBarCheckBox.Checked += (s, e) => { GameBarFix.Enabled = true; GameBarFix.Apply(true); };
                SuppressGameBarCheckBox.Unchecked += (s, e) => { GameBarFix.Enabled = false; GameBarFix.Apply(false); };
                if (GameBarFix.Enabled) GameBarFix.Apply(true);
                UseDiscreteGpuCheckBox.Checked += (s, e) => SaveSettings();
                UseDiscreteGpuCheckBox.Unchecked += (s, e) => SaveSettings();
                AddPluginButton.Click += AddPluginButton_Click;
                JavaArchitectureCheckBox.IsChecked = JavaManager.PreferredArchitecture == JavaArchitecture.X86;
                JavaArchitectureCheckBox.Checked += (s, e) => { JavaManager.PreferredArchitecture = JavaArchitecture.X86; RefreshJavaVersionsList(); };
                JavaArchitectureCheckBox.Unchecked += (s, e) => { JavaManager.PreferredArchitecture = JavaArchitecture.X64; RefreshJavaVersionsList(); };
                RefreshPluginsButton.Click += RefreshPluginsButton_Click;
                CheckUpdatesButton.Click += CheckUpdatesButton_Click;
                OpenGitHubButton.Click += OpenGitHubButton_Click;
                NewsBrowserComboBox.SelectionChanged += NewsBrowserComboBox_SelectionChanged;
                ChangeSkinButton.Click += ChangeSkinButton_Click;
                ResetSkinButton.Click += ResetSkinButton_Click;
                SaveSkinButton.Click += SaveSkinButton_Click;
                CapeComboBox.SelectionChanged += CapeComboBox_SelectionChanged;
                SkinModelSteve.Checked += (s, e) => PreviewSkinModel();
                SkinModelAlex.Checked += (s, e) => PreviewSkinModel();
                AccountsListBox.SelectionChanged += AccountsListBox_SelectionChanged;
                InitializeSharedFiles();
                InitializeVersionToggles();
                InitializeAccent();
                ProxyTextBox.Text = LauncherSettings.ProxyUrl;
                ProxySaveButton.Click += ProxySaveButton_Click;
                ProxyTestButton.Click += ProxyTestButton_Click;
                CustomBrowserTextBox.Text = LauncherSettings.CustomBrowser;
                CustomEditorTextBox.Text = LauncherSettings.CustomEditor;
                CustomBrowserTextBox.LostFocus += (s, e) => LauncherSettings.CustomBrowser = CustomBrowserTextBox.Text.Trim();
                CustomEditorTextBox.LostFocus += (s, e) => LauncherSettings.CustomEditor = CustomEditorTextBox.Text.Trim();
                ShowSettingsPanel("General");
                _settingsReady = true;
                if (DiscordRpcCheckBox.IsChecked == true) After(100, InitializeDiscordRpc);
            }
            catch (Exception ex) { LogMessage($"Error initializing settings: {ex.Message}"); }
        }
        private bool _syncingVersionToggles;
        private void InitializeVersionToggles()
        {
            var groups = new (string Key, CheckBox[] Boxes)[]
            {
                ("showSnapshots", [ProfileSnapshotsCheckBox, SettingsSnapshotsCheckBox]),
                ("showBetas", [ProfileBetasCheckBox, SettingsBetasCheckBox]),
                ("showAlphas", [ProfileAlphasCheckBox, SettingsAlphasCheckBox])
            };
            foreach (var (key, boxes) in groups)
            {
                foreach (var box in boxes)
                {
                    box.IsChecked = LauncherSettings.GetBool(key);
                    var k = key;
                    var all = boxes;
                    RoutedEventHandler changed = (s, e) =>
                    {
                        if (_syncingVersionToggles) return;
                        var value = ((CheckBox)s).IsChecked == true;
                        _syncingVersionToggles = true;
                        foreach (var other in all) other.IsChecked = value;
                        _syncingVersionToggles = false;
                        LauncherSettings.Set(k, value);
                        if (GameProfilesEditView.Visibility == Visibility.Visible) LoadVersionsAsync();
                    };
                    box.Checked += changed;
                    box.Unchecked += changed;
                }
            }
        }
        private static readonly string[] AccentPresets = ["#FF8C00", "#3B82F6", "#22A559", "#E5484D", "#8E4EC6", "#E93D82", "#12A594", "#F5B400", "#8A8A8A"];
        private bool _accentFilling;
        private void InitializeAccent()
        {
            _accentFilling = true;
            AccentComboBox.Items.Add(new ComboBoxItem { Content = L("QT_ACCENT_DEFAULT", "Theme default"), Tag = "" });
            foreach (var hex in AccentPresets)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new System.Windows.Shapes.Rectangle { Width = 14, Height = 14, Margin = new Thickness(0, 0, 8, 0), Fill = (Brush)new BrushConverter().ConvertFromString(hex)!, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = hex, VerticalAlignment = VerticalAlignment.Center });
                AccentComboBox.Items.Add(new ComboBoxItem { Content = row, Tag = hex });
            }
            var current = LauncherSettings.AccentColor;
            AccentHexTextBox.Text = current;
            var match = AccentComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals((string)i.Tag, current, StringComparison.OrdinalIgnoreCase));
            if (match == null && ThemeManager.TryParseColor(current, out _))
            {
                match = new ComboBoxItem { Content = current, Tag = current };
                AccentComboBox.Items.Add(match);
            }
            AccentComboBox.SelectedItem = match ?? AccentComboBox.Items[0];
            _accentFilling = false;
            AccentComboBox.SelectionChanged += (s, e) =>
            {
                if (_accentFilling || AccentComboBox.SelectedItem is not ComboBoxItem item) return;
                SetAccent((string)item.Tag);
            };
            AccentApplyButton.Click += (s, e) =>
            {
                var text = AccentHexTextBox.Text.Trim();
                if (text.Length > 0 && !ThemeManager.TryParseColor(text, out _))
                {
                    OlMessageBox.Show(this, L("QT_ACCENT_INVALID", "Enter a color like #FF8C00."), LocalizedAccentColor, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (text.Length > 0 && !text.StartsWith('#')) text = "#" + text;
                SetAccent(text.ToUpperInvariant());
            };
        }
        private void SetAccent(string hex)
        {
            LauncherSettings.AccentColor = hex;
            ThemeManager.ApplyAccent(hex);
            AccentHexTextBox.Text = hex;
        }
        private void InitializeSharedFiles()
        {
            var toggles = new (CheckBox Box, string Key)[]
            {
                (ShareResourcePacksCheckBox, "shareResourcepacks"),
                (ShareShaderPacksCheckBox, "shareShaderpacks"),
                (ShareOptionsCheckBox, "shareOptions"),
                (ShareServersCheckBox, "shareServers"),
                (ShareScreenshotsCheckBox, "shareScreenshots")
            };
            foreach (var (box, key) in toggles)
            {
                box.IsChecked = LauncherSettings.GetBool(key);
                var k = key;
                box.Checked += (s, e) => { LauncherSettings.Set(k, true); ApplySharedFiles(); };
                box.Unchecked += (s, e) => { LauncherSettings.Set(k, false); ApplySharedFiles(); };
            }
            SharedApplyButton.Click += (s, e) => ApplySharedFiles();
            SharedOpenButton.Click += (s, e) => UiKit.OpenPath(SharingManager.SharedDir);
        }
        private async void ApplySharedFiles()
        {
            if (_launch != null)
            {
                SharedStatusText.Text = L("LAUNCHER_BUSY_MSG", "A launch is already in progress.");
                return;
            }
            SharedApplyButton.IsEnabled = false;
            try
            {
                await Task.Run(SharingManager.ApplyAll);
                SharedStatusText.Text = L("SETTINGS_SHARED_UPDATED", "Shared folders updated for all instances.");
            }
            catch (Exception ex)
            {
                SharedStatusText.Text = $"{L("ERROR", "Error")}: {ex.Message}";
            }
            finally
            {
                SharedApplyButton.IsEnabled = true;
                LoadResourcePacks();
                LoadShaderPacks();
            }
        }
        private void ProxySaveButton_Click(object sender, RoutedEventArgs e)
        {
            LauncherSettings.ProxyUrl = ProxyTextBox.Text.Trim();
            var (_, message) = NetworkProxy.Apply();
            ProxyStatusText.Text = message;
        }
        private async void ProxyTestButton_Click(object sender, RoutedEventArgs e)
        {
            ProxySaveButton_Click(sender, e);
            ProxyTestButton.IsEnabled = false;
            ProxyStatusText.Text = L("QT_LOADING", "Loading...");
            var (_, message) = await NetworkProxy.TestAsync("https://api.modrinth.com/v2/tag/loader");
            ProxyStatusText.Text = message;
            ProxyTestButton.IsEnabled = true;
        }
        private async void AccountsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => await LoadCurrentAccountSkinAsync();
        private void RefreshJavaVersionsList()
        {
            var installText = L("SETTINGS_JAVA_INSTALL", "Install");
            var uninstallText = L("SETTINGS_JAVA_UNINSTALL", "Uninstall");
            var installedText = L("SETTINGS_JAVA_INSTALLED", "Installed");
            var notInstalledText = L("SETTINGS_JAVA_NOT_INSTALLED", "Not installed");
            var items = new List<JavaVersionViewModel>();
            foreach (var ver in JavaManager.GetAvailableVersions())
            {
                bool installed = JavaManager.IsJavaInstalled(ver);
                var arch = JavaManager.ResolveArchitecture(ver);
                var fellBack = arch != JavaManager.PreferredArchitecture;
                var archLabel = arch == JavaArchitecture.X86 ? "x86" : "x64";
                items.Add(new JavaVersionViewModel
                {
                    Version = ver,
                    Label = fellBack
                        ? L("SETTINGS_JAVA_LABEL_FALLBACK", "Java {version} (Adoptium, {arch}, no 32-bit build available)", ("version", ver), ("arch", archLabel))
                        : L("SETTINGS_JAVA_LABEL", "Java {version} (Adoptium, {arch})", ("version", ver), ("arch", archLabel)),
                    Status = installed ? installedText : notInstalledText,
                    InstallButtonText = installText,
                    UninstallButtonText = uninstallText,
                    ShowInstall = !installed,
                    ShowUninstall = installed
                });
            }
            JavaVersionsItemsControl.ItemsSource = items;
        }
        private async void InstallJavaButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int version) return;
            Action<string> progress = msg => SetStatus(msg);
            try
            {
                btn.IsEnabled = false;
                StatusText = L("QT_INSTALLING_JAVA", "Installing Java {version}...", ("version", version));
                JavaManager.OnProgressChanged += progress;
                await JavaManager.InstallJavaAsync(version);
                LogMessage($"Java {version} installed successfully");
                RefreshJavaVersionsList();
                StatusText = L("QT_JAVA_INSTALLED", "Java {version} installed", ("version", version));
            }
            catch (Exception ex)
            {
                OlMessageBox.Show(this, ex.Message, LocalizedJavaInstallation, MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"Java {version} install failed: {ex.Message}");
                StatusText = L("QT_READY", "Ready");
            }
            finally
            {
                JavaManager.OnProgressChanged -= progress;
                btn.IsEnabled = true;
            }
        }
        private void UninstallJavaButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int version) return;
            try
            {
                JavaManager.UninstallJava(version);
                LogMessage($"Java {version} uninstalled");
                RefreshJavaVersionsList();
            }
            catch (Exception ex)
            {
                OlMessageBox.Show(this, ex.Message, LocalizedJavaInstallation, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RefreshJavaComboBoxInProfile()
        {
            var items = new List<ComboBoxItem> { new() { Content = L("SETTINGS_JAVA_AUTO", "Automatic (Detect)"), Tag = (string?)null } };
            foreach (var java in JavaManager.GetAllJavaInstallations())
                items.Add(new ComboBoxItem { Content = java.DisplayName, Tag = java.Path });
            ProfileJavaComboBox.ItemsSource = items;
            ProfileJavaComboBox.SelectedIndex = 0;
        }
        private void ShowSettingsPanel(string panelName)
        {
            GeneralSettingsPanel.Visibility = Visibility.Collapsed;
            AccountsSettingsPanel.Visibility = Visibility.Collapsed;
            AdvancedSettingsPanel.Visibility = Visibility.Collapsed;
            AboutSettingsPanel.Visibility = Visibility.Collapsed;
            foreach (var b in new[] { GeneralSettingsButton, AccountsSettingsButton, AdvancedSettingsButton, AboutSettingsButton })
            {
                b.ClearValue(Control.BackgroundProperty);
                b.ClearValue(Control.ForegroundProperty);
                b.ClearValue(Control.BorderBrushProperty);
            }
            void Mark(Button b)
            {
                b.SetResourceReference(Control.BackgroundProperty, "BgSectionBrush");
                b.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush");
                b.SetResourceReference(Control.BorderBrushProperty, "AccentPrimaryBrush");
            }
            switch (panelName)
            {
                case "General": GeneralSettingsPanel.Visibility = Visibility.Visible; Mark(GeneralSettingsButton); break;
                case "Accounts":
                    AccountsSettingsPanel.Visibility = Visibility.Visible;
                    Mark(AccountsSettingsButton);
                    _ = LoadCurrentAccountSkinAsync();
                    break;
                case "Advanced": AdvancedSettingsPanel.Visibility = Visibility.Visible; Mark(AdvancedSettingsButton); RefreshJavaVersionsList(); break;
                case "About": AboutSettingsPanel.Visibility = Visibility.Visible; Mark(AboutSettingsButton); break;
            }
        }
        private void LoadSettingsData()
        {
            var langCode = LauncherSettings.Language;
            foreach (ComboBoxItem item in LanguageComboBox.Items)
                if (item.Tag?.ToString() == langCode) { LanguageComboBox.SelectedItem = item; break; }
            if (LanguageComboBox.SelectedIndex < 0) LanguageComboBox.SelectedIndex = 0;
            var themeName = LauncherSettings.Theme;
            foreach (ComboBoxItem item in ThemeComboBox.Items)
                if (item.Tag?.ToString() == themeName) { ThemeComboBox.SelectedItem = item; break; }
            if (ThemeComboBox.SelectedIndex < 0) ThemeComboBox.SelectedIndex = 0;
            DiscordRpcCheckBox.IsChecked = LauncherSettings.DiscordRpcEnabled;
            DeleteTelemetryCheckBox.IsChecked = LauncherSettings.DeleteTelemetryOnStartup;
            ModernTitleBarCheckBox.IsChecked = LauncherSettings.ModernTitleBar;
            DebugModeCheckBox.IsChecked = LauncherSettings.DebugModeEnabled;
            DebugInfoTextBox.Visibility = LauncherSettings.DebugModeEnabled ? Visibility.Visible : Visibility.Collapsed;
            UpdateDebugInfo();
            var browser = LauncherSettings.NewsBrowser;
            foreach (ComboBoxItem item in NewsBrowserComboBox.Items)
                if (item.Tag?.ToString() == browser) { NewsBrowserComboBox.SelectedItem = item; break; }
            if (NewsBrowserComboBox.SelectedIndex < 0) NewsBrowserComboBox.SelectedIndex = 0;
            UseDiscreteGpuCheckBox.IsChecked = LauncherSettings.UseDiscreteGpu;
        }
        private void LoadAccountsData()
        {
            try
            {
                var selected = (AccountsListBox.SelectedItem as UserProfile)?.Uuid;
                _userProfiles.Clear();
                foreach (var profile in ProfileManager.Instance.GetProfiles()) _userProfiles.Add(profile);
                AccountsListBox.ItemsSource = _userProfiles;
                if (selected != null) AccountsListBox.SelectedItem = _userProfiles.FirstOrDefault(p => p.Uuid == selected);
            }
            catch { }
        }
        private void LoadPluginsData()
        {
            try
            {
                _plugins.Clear();
                var pluginsPath = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_plugins.json");
                if (File.Exists(pluginsPath))
                {
                    var plugins = JsonSerializer.Deserialize<List<PluginInfo>>(File.ReadAllText(pluginsPath));
                    if (plugins != null) foreach (var p in plugins) _plugins.Add(p);
                }
                PluginsListBox.ItemsSource = _plugins;
            }
            catch { }
        }
        private void ModernTitleBarChanged()
        {
            if (!_settingsReady) return;
            var value = ModernTitleBarCheckBox.IsChecked == true;
            if (value == LauncherSettings.ModernTitleBar) return;
            LauncherSettings.ModernTitleBar = value;
            if (OlMessageBox.Show(this, L("SETTINGS_TITLEBAR_RESTART", "The title bar changes after a restart. Restart the launcher now?"), LocalizedTitleBar, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                RestartApplication();
        }
        private void SaveSettings()
        {
            if (!_settingsReady) return;
            LauncherSettings.Language = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en-US";
            LauncherSettings.Theme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "arc";
            LauncherSettings.DiscordRpcEnabled = DiscordRpcCheckBox.IsChecked ?? true;
            LauncherSettings.DeleteTelemetryOnStartup = DeleteTelemetryCheckBox.IsChecked ?? false;
            LauncherSettings.DebugModeEnabled = DebugModeCheckBox.IsChecked ?? false;
            LauncherSettings.NewsBrowser = (NewsBrowserComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "webview2";
            LauncherSettings.UseDiscreteGpu = UseDiscreteGpuCheckBox.IsChecked ?? false;
        }
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string languageCode) return;
            if (languageCode == LocalizationManager.CurrentLanguage) return;
            SaveSettings();
            LocalizationManager.LoadLanguage(languageCode);
            RefreshLocalizedStrings();
            ApplyNewUiLocalization();
            if (OlMessageBox.Show(this, L("LANGUAGE_CHANGED_MSG", "Language changed. Restart the launcher now to apply it everywhere?"), L("LANGUAGE_CHANGED_TITLE", "Language changed"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                RestartApplication();
        }
        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string themeKey)
            {
                ThemeManager.ApplyTheme(themeKey);
                SaveSettings();
            }
        }
        private void AddMicrosoftAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var login = new MicrosoftLoginDialog { Owner = this };
            if (login.ShowDialog() == true && login.ResultProfile != null)
            {
                ProfileManager.Instance.AddOrUpdateProfile(login.ResultProfile, true);
                OlMessageBox.Show(this, L("QT_ACCOUNT_ADDED", "Added account {name}", ("name", login.ResultProfile.Username)), LocalizedAccounts, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (login.Error != null)
            {
                OlMessageBox.Show(this, $"{L("MS_AUTH_FAIL", "Authentication failed")}\n{login.Error}", L("MS_AUTH_ERROR", "Microsoft sign-in"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AddOfflineAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AddTextDialog(L("OFFLINE_ACCOUNT_PROMPT", "Username:"), L("OFFLINE_ACCOUNT_TITLE", "Offline account"), "") { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.ResultText))
            {
                var profile = ProfileManager.Instance.AddProfile(dlg.ResultText.Trim(), "Offline");
                ProfileManager.Instance.SetSelectedProfile(profile.Uuid);
            }
        }
        private void DebugModeCheckBox_Checked(object sender, RoutedEventArgs e) { DebugInfoTextBox.Visibility = Visibility.Visible; SaveSettings(); UpdateDebugInfo(); }
        private void DebugModeCheckBox_Unchecked(object sender, RoutedEventArgs e) { DebugInfoTextBox.Visibility = Visibility.Collapsed; SaveSettings(); }
        private void AddPluginButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"{L("PLUGIN_FILES", "Plugin DLL files")} (*.dll)|*.dll",
                Title = L("PLUGIN_SELECT_TITLE", "Select Plugin File")
            };
            if (dlg.ShowDialog(this) == true)
            {
                _plugins.Add(new PluginInfo { Path = dlg.FileName, IsEnabled = true });
                SavePlugins();
                LoadEnabledPlugins();
            }
        }
        private void RefreshPluginsButton_Click(object sender, RoutedEventArgs e) { LoadPluginsData(); LoadEnabledPlugins(); }
        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckUpdatesButton.IsEnabled = false;
                StatusText = L("QT_CHECKING_UPDATES", "Checking for updates...");
                var info = await UpdateManager.CheckForUpdatesAsync();
                if (info.UpdateAvailable)
                {
                    if (!UpdateManager.IsInstalledBuild())
                    {
                        if (OlMessageBox.Show(this, L("QT_UPDATE_PORTABLE_MSG", "OrangLauncher is running from a folder, so the built-in updater is disabled.\n\nInstall OrangLauncher with the MSI package from the download page — installed copies update automatically.\n\nOpen the download page now?"), L("QT_UPDATE_PORTABLE_TITLE", "Updates unavailable"), MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                            UpdateManager.OpenReleasePage(info);
                        return;
                    }
                    var notes = info.ReleaseNotes ?? "";
                    if (notes.Length > 1500) notes = notes[..1500];
                    if (OlMessageBox.Show(this, L("QT_UPDATE_MSG", "A new version ({version}) is available!\n\nRelease notes:\n{notes}\n\nDo you want to update now?", ("version", info.NewVersion), ("notes", notes)), L("QT_UPDATE_AVAILABLE_TITLE", "Update available"), MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        UpdateManager.ProgressChanged += m => SetStatus(m);
                        if (await UpdateManager.DownloadAndInstallUpdateAsync(info))
                            Application.Current.Shutdown();
                    }
                }
                else
                {
                    OlMessageBox.Show(this, L("QT_LATEST_VERSION", "You are using the latest version ({version}).", ("version", UpdateManager.GetCurrentVersionString())), L("QT_NO_UPDATES", "No updates"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                OlMessageBox.Show(this, ex.Message, L("QT_UPDATES", "Updates"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StatusText = L("QT_READY", "Ready");
                CheckUpdatesButton.IsEnabled = true;
            }
        }
        private async void AutoCheckUpdates()
        {
            try
            {
                var info = await UpdateManager.CheckForUpdatesAsync();
                if (info.UpdateAvailable && _launch == null && UpdateManager.IsInstalledBuild())
                    StatusText = L("QT_UPDATE_AVAILABLE", "Update available: {version} (Settings > About)", ("version", info.NewVersion));
            }
            catch { }
        }
        private void DeleteTelemetryIfEnabled()
        {
            if (!LauncherSettings.DeleteTelemetryOnStartup) return;
            Task.Run(() =>
            {
                int n = 0;
                foreach (var inst in InstanceManager.Instance.GetInstances())
                {
                    foreach (var sub in new[] { System.IO.Path.Combine("logs", "telemetry"), "telemetry" })
                    {
                        var d = System.IO.Path.Combine(inst.MinecraftDir, sub);
                        try
                        {
                            if (Directory.Exists(d) && !FileLinks.IsLink(d)) { Directory.Delete(d, true); n++; }
                        }
                        catch { }
                    }
                }
                if (n > 0) AppendLog($"[Privacy] Removed telemetry from {n} folder(s)");
            });
        }
        private void NewsBrowserComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NewsBrowserComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string browser) return;
            var previousBrowser = LauncherSettings.NewsBrowser;
            if (!_settingsReady || browser == previousBrowser) return;
            SaveSettings();
            if (browser == "ie" || previousBrowser == "ie")
            {
                OlMessageBox.Show(this, L("QT_RESTART_BROWSER", "The launcher will now restart to apply browser changes."), L("QT_RESTART_REQUIRED", "Restart required"), MessageBoxButton.OK, MessageBoxImage.Information);
                RestartApplication();
            }
            else
            {
                ApplyNewsBrowser(browser);
            }
        }
        private void OpenGitHubButton_Click(object sender, RoutedEventArgs e) => UpdateManager.OpenGitHubPage();
        private void SavePlugins()
        {
            try
            {
                var path = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_plugins.json");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(_plugins.ToList(), new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
        private void LoadEnabledPlugins()
        {
            try
            {
                PluginManager.Instance.SetLogAction(LogMessage);
                PluginManager.Instance.LoadPlugins(_plugins);
                LogMessage($"Loaded {PluginManager.Instance.LoadedPlugins.Count} plugin(s)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading plugins: {ex.Message}");
            }
        }
        private void UpdateDebugInfo()
        {
            if (DebugInfoTextBox.Visibility == Visibility.Visible)
                DebugInfoTextBox.Text = $"OS: {Environment.OSVersion}\n.NET: {Environment.Version}\nData: {PlatformPaths.GetDataDir()}\nLog: {_debugLog}";
        }
        private void RestartApplication()
        {
            var path = Environment.ProcessPath;
            if (path == null) return;
            Process.Start(path);
            Application.Current.Shutdown();
        }
        private async Task InitializeNewsBrowser()
        {
            try
            {
                var browser = LauncherSettings.NewsBrowser;
                if (browser != "ie" && browser != "native")
                {
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
            }
            catch (Exception ex)
            {
                LogMessage($"Error during browser initialization: {ex.Message}");
            }
        }
        private void ApplyNewsBrowserFromSettings() => ApplyNewsBrowser(LauncherSettings.NewsBrowser);
        private void ApplyNewsBrowser(string browser)
        {
            try
            {
                string url = "https://oranges.lt/launcher.html";
                if (browser == "native")
                {
                    try { NewsWebBrowser.Navigate("about:blank"); } catch { }
                    NewsWebView.Visibility = Visibility.Collapsed;
                    NewsWebBrowser.Visibility = Visibility.Collapsed;
                    NewsNativePanel.Visibility = Visibility.Visible;
                    _ = LoadNativeNewsAsync(url);
                    return;
                }
                NewsNativePanel.Visibility = Visibility.Collapsed;
                if (browser == "ie")
                {
                    try
                    {
                        if (NewsWebView.CoreWebView2 != null)
                        {
                            NewsWebView.CoreWebView2.Stop();
                            NewsWebView.Dispose();
                        }
                    }
                    catch { }
                    NewsWebView.Visibility = Visibility.Collapsed;
                    NewsWebBrowser.Visibility = Visibility.Visible;
                    NewsWebBrowser.Navigating += (s, e) => SuppressScriptErrors(NewsWebBrowser, true);
                    SuppressScriptErrors(NewsWebBrowser, true);
                    NewsWebBrowser.Navigated -= NewsWebBrowser_Navigated;
                    NewsWebBrowser.Navigated += NewsWebBrowser_Navigated;
                    NewsWebBrowser.LoadCompleted -= NewsWebBrowser_LoadCompleted;
                    NewsWebBrowser.LoadCompleted += NewsWebBrowser_LoadCompleted;
                    NewsWebBrowser.Navigate(url);
                }
                else
                {
                    try { NewsWebBrowser.Navigate("about:blank"); } catch { }
                    NewsWebBrowser.Visibility = Visibility.Collapsed;
                    NewsWebView.Visibility = Visibility.Visible;
                    if (NewsWebView.CoreWebView2 != null) NewsWebView.CoreWebView2.Navigate(url);
                    else NewsWebView.Source = new Uri(url);
                }
            }
            catch { }
        }
        private void NewsNativeBrowserLink_Click(object sender, RoutedEventArgs e) => UiKit.OpenUrl("https://oranges.lt/launcher.html");
        private static readonly HttpClient _newsHttp = new() { Timeout = TimeSpan.FromSeconds(20) };
        private async Task LoadNativeNewsAsync(string url)
        {
            NewsNativeTitle.Text = L("UPDATE_NOTES", "Update Notes");
            NewsNativeBrowserText.Text = L("NEWS_OPEN_BROWSER", "Open news in browser");
            NewsNativeStack.Children.Clear();
            try
            {
                NewsNativeStack.Children.Add(NewsParagraph(L("NEWS_LOADING", "Loading news...")));
                var html = await _newsHttp.GetStringAsync(url);
                NewsNativeStack.Children.Clear();
                foreach (var el in BuildNewsBlocks(html)) NewsNativeStack.Children.Add(el);
                if (NewsNativeStack.Children.Count == 0)
                    NewsNativeStack.Children.Add(NewsParagraph(L("CONTENT_NO_RESULTS", "No results.")));
            }
            catch (Exception ex)
            {
                NewsNativeStack.Children.Clear();
                NewsNativeStack.Children.Add(NewsParagraph($"{L("NEWS_LOAD_FAILED", "Could not load news:")} {ex.Message}"));
            }
        }
        private TextBlock NewsParagraph(string text) => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Opacity = 0.9,
            Margin = new Thickness(0, 2, 0, 2),
            Foreground = (Brush)FindResource("FgPrimaryBrush")
        };
        private IEnumerable<UIElement> BuildNewsBlocks(string html)
        {
            var blocks = new List<UIElement>();
            html = Regex.Replace(html, "<(script|style|head)[^>]*>.*?</\\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var matches = Regex.Matches(html, "<(?<tag>h[1-6]|p|li)[^>]*>(?<body>.*?)</\\k<tag>>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            bool first = true;
            foreach (Match m in matches)
            {
                var tag = m.Groups["tag"].Value.ToLowerInvariant();
                var text = System.Net.WebUtility.HtmlDecode(Regex.Replace(m.Groups["body"].Value, "<[^>]+>", " "));
                text = Regex.Replace(text, "\\s+", " ").Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;
                switch (tag)
                {
                    case "h1":
                    case "h2":
                        if (!first)
                            blocks.Add(new Border { Height = 1, Margin = new Thickness(0, 14, 0, 10), Background = (Brush)FindResource("BorderBrush") });
                        var heading = NewsParagraph(text);
                        heading.FontSize = 18;
                        heading.FontWeight = FontWeights.Bold;
                        heading.Margin = new Thickness(0, 2, 0, 4);
                        blocks.Add(heading);
                        break;
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        var sub = NewsParagraph(text);
                        sub.FontSize = 15;
                        sub.FontWeight = FontWeights.SemiBold;
                        sub.Margin = new Thickness(0, 8, 0, 2);
                        blocks.Add(sub);
                        break;
                    case "li":
                        blocks.Add(NewsParagraph($"•  {text}"));
                        break;
                    default:
                        blocks.Add(NewsParagraph(text));
                        break;
                }
                first = false;
            }
            return blocks;
        }
        private void NewsWebBrowser_Navigated(object sender, NavigationEventArgs e)
        {
            SuppressScriptErrors(NewsWebBrowser, true);
            InjectScriptErrorHandler(NewsWebBrowser);
        }
        private void NewsWebBrowser_LoadCompleted(object sender, NavigationEventArgs e)
        {
            SuppressScriptErrors(NewsWebBrowser, true);
            InjectScriptErrorHandler(NewsWebBrowser);
        }
        private static void SuppressScriptErrors(WebBrowser webBrowser, bool hide)
        {
            try
            {
                var field = typeof(WebBrowser).GetField("_axIWebBrowser2", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field == null) return;
                object? com = field.GetValue(webBrowser);
                if (com == null)
                {
                    webBrowser.Navigated += (s, e) =>
                    {
                        try
                        {
                            var obj = field.GetValue(webBrowser);
                            obj?.GetType().InvokeMember("Silent", System.Reflection.BindingFlags.SetProperty, null, obj, [hide]);
                        }
                        catch { }
                    };
                    return;
                }
                com.GetType().InvokeMember("Silent", System.Reflection.BindingFlags.SetProperty, null, com, [hide]);
            }
            catch { }
        }
        private static void InjectScriptErrorHandler(WebBrowser webBrowser)
        {
            try
            {
                if (webBrowser.Document != null)
                    webBrowser.InvokeScript("eval", ["window.onerror = function(msg, url, line) { return true; };"]);
            }
            catch { }
        }
        private UserProfile? SkinAccount()
        {
            if (AccountsListBox.SelectedItem is UserProfile profile) return profile;
            if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile comboProfile) return comboProfile;
            return ProfileManager.Instance.GetSelectedProfile();
        }
        private UserProfile? MicrosoftSkinAccount()
        {
            var account = SkinAccount();
            if (account is { IsMicrosoft: true } && !string.IsNullOrEmpty(account.AccessToken ?? account.MinecraftToken)) return account;
            return null;
        }
        private int _skinLoad;
        private bool _capeFilling;
        private async Task LoadCurrentAccountSkinAsync()
        {
            var account = SkinAccount();
            var token = ++_skinLoad;
            _skinData = null;
            SaveSkinButton.IsEnabled = false;
            _capeFilling = true;
            CapeComboBox.Items.Clear();
            CapeComboBox.IsEnabled = false;
            _capeFilling = false;
            var isMs = account?.IsMicrosoft == true;
            ChangeSkinButton.IsEnabled = isMs;
            ResetSkinButton.IsEnabled = isMs;
            if (account == null || string.IsNullOrEmpty(account.Username))
            {
                SkinInfoText.Text = LocalizedSelectAccountToView;
                SkinBodyImage.Source = null;
                SkinBodyViewer.Clear();
                return;
            }
            try
            {
                SkinInfoText.Text = L("QT_LOADING_SKIN", "Loading skin...");
                var data = await SkinService.LoadAsync(account);
                if (token != _skinLoad) return;
                if (data?.Skin == null)
                {
                    SkinInfoText.Text = L("QT_SKIN_NOT_FOUND", "Could not find player or skin data.");
                    SkinBodyViewer.Clear();
                    return;
                }
                _skinData = data;
                bool rendered3d = SkinBodyViewer.LoadSkin(data.Skin, data.Slim);
                if (rendered3d) SkinBodyViewer.LoadCape(data.Cape);
                SkinBodyViewer.Visibility = rendered3d ? Visibility.Visible : Visibility.Collapsed;
                SkinBodyImage.Visibility = rendered3d ? Visibility.Collapsed : Visibility.Visible;
                if (!rendered3d) SkinBodyImage.Source = UiKit.ImageFromBytes(data.Skin);
                _capeFilling = true;
                if (data.Slim) SkinModelAlex.IsChecked = true; else SkinModelSteve.IsChecked = true;
                if (isMs && data.FromAccount)
                {
                    CapeComboBox.Items.Add(new ComboBoxItem { Content = L("QT_NO_CAPE", "No cape"), Tag = null });
                    foreach (var cape in data.Capes)
                        CapeComboBox.Items.Add(new ComboBoxItem { Content = string.IsNullOrEmpty(cape.Alias) ? cape.Id : cape.Alias, Tag = cape });
                    var active = data.Capes.FirstOrDefault(c => c.Active);
                    CapeComboBox.SelectedIndex = active == null ? 0 : data.Capes.IndexOf(active) + 1;
                    CapeComboBox.IsEnabled = data.Capes.Count > 0;
                }
                _capeFilling = false;
                SaveSkinButton.IsEnabled = true;
                var capeText = data.Cape != null ? L("QT_YES", "Yes") : L("QT_NO", "No");
                SkinInfoText.Text = L("QT_SKIN_INFO", "Username: {username}\nUUID: {uuid}\nModel: {model}\nHas cape: {cape}",
                    ("username", data.Username), ("uuid", data.Uuid), ("model", data.Slim ? LocalizedAlexSlim : LocalizedSteveClassic), ("cape", capeText));
                if (!isMs) SkinInfoText.Text += "\n\n" + L("QT_SKIN_MS_ONLY", "Uploading a skin or changing the cape needs a Microsoft account.");
            }
            catch (Exception ex)
            {
                _capeFilling = false;
                SkinInfoText.Text = $"{L("ERROR", "Error")}: {ex.Message}";
            }
        }
        private void PreviewSkinModel()
        {
            if (_capeFilling || _skinData?.Skin == null) return;
            if (SkinBodyViewer.LoadSkin(_skinData.Skin, SkinModelAlex.IsChecked == true)) SkinBodyViewer.LoadCape(_skinData.Cape);
        }
        private async void CapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_capeFilling || CapeComboBox.SelectedItem is not ComboBoxItem item) return;
            var account = MicrosoftSkinAccount();
            if (account == null) return;
            var cape = item.Tag as CapeInfo;
            try
            {
                CapeComboBox.IsEnabled = false;
                SkinInfoText.Text = L("QT_CAPE_CHANGING", "Changing cape...");
                await SkinService.SetActiveCapeAsync(account, cape?.Id);
                await LoadCurrentAccountSkinAsync();
            }
            catch (Exception ex)
            {
                SkinInfoText.Text = $"{L("ERROR", "Error")}: {ex.Message}";
                CapeComboBox.IsEnabled = true;
            }
        }
        private void SaveSkinButton_Click(object sender, RoutedEventArgs e)
        {
            if (_skinData?.Skin == null) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = LocalizedSaveSkin,
                Filter = "PNG (*.png)|*.png",
                FileName = $"{_skinData.Username}.png"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllBytes(dlg.FileName, _skinData.Skin);
                StatusText = L("QT_SKIN_SAVED", "Skin saved to {path}", ("path", dlg.FileName));
            }
            catch (Exception ex)
            {
                OlMessageBox.Show(this, ex.Message, LocalizedSaveSkin, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void ResetSkinButton_Click(object sender, RoutedEventArgs e)
        {
            var account = MicrosoftSkinAccount();
            if (account == null)
            {
                OlMessageBox.Show(this, L("QT_SKIN_MS_ONLY", "Uploading a skin or changing the cape needs a Microsoft account."), LocalizedResetSkin, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (OlMessageBox.Show(this, L("QT_RESET_SKIN_Q", "Reset your skin to the default?"), LocalizedResetSkin, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            try
            {
                ResetSkinButton.IsEnabled = false;
                SkinInfoText.Text = L("QT_RESETTING_SKIN", "Resetting skin...");
                await SkinService.ResetSkinAsync(account);
                await Task.Delay(1500);
                await LoadCurrentAccountSkinAsync();
            }
            catch (Exception ex)
            {
                SkinInfoText.Text = $"{L("ERROR", "Error")}: {ex.Message}";
            }
            finally
            {
                ResetSkinButton.IsEnabled = true;
            }
        }
        private async void ChangeSkinButton_Click(object sender, RoutedEventArgs e)
        {
            var account = MicrosoftSkinAccount();
            if (account == null)
            {
                OlMessageBox.Show(this, L("QT_SKIN_MS_ONLY", "Uploading a skin or changing the cape needs a Microsoft account."), LocalizedUploadSkin, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var pick = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PNG (*.png)|*.png",
                Title = L("QT_SELECT_SKIN", "Select a Minecraft skin (64x64 or 64x32 PNG)")
            };
            if (pick.ShowDialog(this) != true) return;
            if (!SkinService.IsValidSkinFile(pick.FileName))
            {
                OlMessageBox.Show(this, L("QT_INVALID_SKIN", "That file is not a valid Minecraft skin (64x64 or 64x32 PNG)."), LocalizedUploadSkin, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var preview = new SkinUploadDialog(pick.FileName, SkinModelAlex.IsChecked == true);
            if (preview.ShowDialog() != true) return;
            try
            {
                ChangeSkinButton.IsEnabled = false;
                SkinInfoText.Text = L("QT_UPLOADING_SKIN", "Uploading skin...");
                await SkinService.UploadSkinAsync(account, pick.FileName, preview.Slim);
                await Task.Delay(1500);
                await LoadCurrentAccountSkinAsync();
            }
            catch (Exception ex)
            {
                SkinInfoText.Text = $"{L("ERROR", "Error")}: {ex.Message}";
            }
            finally
            {
                ChangeSkinButton.IsEnabled = true;
}   }   }   }