using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using OrangLauncher.Backend;
using OrangLauncher.Managers;
using OrangLauncher.Models;
using OrangLauncher.ViewModels;
using System.Globalization;
using System.Windows.Interop;
namespace OrangLauncher
{
    public class InstanceIconConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MinecraftInstance instance)
            {
                if (!string.IsNullOrEmpty(instance.Icon) && File.Exists(instance.Icon))
                {
                     try 
                     {
                        BitmapImage bitmap = new();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(instance.Icon, UriKind.Absolute);
                        bitmap.EndInit();
                        return bitmap;
                     } 
                     catch {}
                }
                string loaderLower = instance.ModLoader?.Trim().ToLowerInvariant() ?? "";
                bool isModded = loaderLower is not ("" or "vanilla" or "none");
                // if we don't have art for that specific loader fall back to the generic modded block.
                var candidateNames = isModded
                    ? new[] { System.IO.Path.Combine("loaders", $"{loaderLower}.png"), "minecraft-blue.png" }
                    : new[] { "minecraft-green.png" };
                foreach (var iconName in candidateNames)
                {
                    string[] checkPaths =
                    [
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "oranglaunch", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "oranglaunch", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "oranglaunch", "images", iconName),
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "launcher", "OrangLauncher", "OrangLauncher", "Other", "images", iconName),
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "launcher", "oranglaunch", "images", iconName)
                    ];
                    foreach (var path in checkPaths)
                    {
                         try
                         {
                             var fullPath = System.IO.Path.GetFullPath(path);
                             if (File.Exists(fullPath))
                             {
                                BitmapImage bitmap = new();
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                                bitmap.EndInit();
                                return bitmap;
                             }
                         }
                         catch {}
                    }
                }
            }
            return null;
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
                    bi.StreamSource = new MemoryStream(binaryData);
                    bi.EndInit();
                    return bi;
                }
                catch { }
            }
            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class UrlToImageConverter : IValueConverter
    {
        private static readonly Dictionary<string, BitmapImage> _imageCache = [];
        private static readonly HttpClient _iconHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        static UrlToImageConverter()
        {
            _iconHttpClient.DefaultRequestHeaders.Add("User-Agent", "OrangLauncher/1.0");
        }
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                if (_imageCache.TryGetValue(url, out var cachedImage))
                    return cachedImage;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.EndInit();
                if (bitmap.CanFreeze) bitmap.Freeze();
                _imageCache[url] = bitmap;
                return bitmap;
            }
            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
            => throw new NotImplementedException();
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
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
        private string _statusText = "Welcome";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
        private string _gameProfileInfo = "No profile selected";
        public string GameProfileInfo { get => _gameProfileInfo; set { _gameProfileInfo = value; OnPropertyChanged(); } }
        public string LocalizedUpdateNotes => LocalizationManager.GetString("UPDATE_NOTES", "Update Notes");
        public string LocalizedLauncherLog => LocalizationManager.GetString("LAUNCHER_LOG", "Launcher Log");
        public string LocalizedGameProfiles => LocalizationManager.GetString("GAME_PROFILES", "Game Profiles");
        public string LocalizedMods => LocalizationManager.GetString("MODS", "Mods");
        public string LocalizedResourcePacks => LocalizationManager.GetString("RESOURCE_PACKS", "Resource & Shader Packs");
        public string LocalizedSettings => LocalizationManager.GetString("SETTINGS", "Settings");
        public string LocalizedProfile => LocalizationManager.GetString("PROFILE", "PROFILE");
        public string LocalizedGameProfilesTitle => LocalizationManager.GetString("GAME_PROFILES_TITLE", "GAME PROFILES");
        public string LocalizedPlay => LocalizationManager.GetString("PLAY", "PLAY");
        public string LocalizedNewProfile => LocalizationManager.GetString("NEW_PROFILE", "NEW PROFILE");
        public string LocalizedGeneral => LocalizationManager.GetString("GENERAL", "General");
        public string LocalizedAccounts => LocalizationManager.GetString("ACCOUNTS", "Accounts");
        public string LocalizedAdvanced => LocalizationManager.GetString("ADVANCED", "Advanced");
        public string LocalizedAbout => LocalizationManager.GetString("ABOUT", "About");
        public string LocalizedClearLog => LocalizationManager.GetString("CLEAR_LOG", "Clear Log");
        public string LocalizedSelect => LocalizationManager.GetString("SELECT", "Select");
        public string LocalizedEdit => LocalizationManager.GetString("EDIT", "Edit");
        public string LocalizedDuplicate => LocalizationManager.GetString("DUPLICATE", "Duplicate");
        public string LocalizedOpenFolder => LocalizationManager.GetString("OPEN_FOLDER", "Open Folder");
        public string LocalizedDelete => LocalizationManager.GetString("DELETE", "Delete");
        public string LocalizedInstanceSelect => LocalizationManager.GetString("INSTANCE_SELECT", "Select");
        public string LocalizedInstanceEdit => LocalizationManager.GetString("INSTANCE_EDIT", "Edit");
        public string LocalizedInstanceDuplicate => LocalizationManager.GetString("INSTANCE_DUPLICATE", "Duplicate");
        public string LocalizedInstanceOpenFolder => LocalizationManager.GetString("INSTANCE_OPEN_FOLDER", "Open Folder");
        public string LocalizedInstanceDelete => LocalizationManager.GetString("INSTANCE_DELETE", "Delete");
        public string LocalizedCreateProfile => LocalizationManager.GetString("GAME_PROFILES_CREATE_TITLE", "Create Profile");
        public string LocalizedEditProfile => LocalizationManager.GetString("GAME_PROFILES_EDIT_TITLE", "Edit Profile");
        public string LocalizedName => LocalizationManager.GetString("GAME_PROFILES_NAME", "Name");
        public string LocalizedVersion => LocalizationManager.GetString("GAME_PROFILES_VERSION", "Version");
        public string LocalizedModLoader => LocalizationManager.GetString("GAME_PROFILES_LOADER", "Mod Loader");
        public string LocalizedLoaderVersion => LocalizationManager.GetString("GAME_PROFILES_LOADER_VERSION", "Loader Version");
        public string LocalizedRam => LocalizationManager.GetString("GAME_PROFILES_RAM", "RAM");
        public string LocalizedJavaArgs => LocalizationManager.GetString("STARTUP_ARGS", "Startup Arguments");
        public string LocalizedJavaArgsDesc => LocalizationManager.GetString("PROFILE_JAVA_ARGS_DESC", "Shows all arguments that will be used. Fully editable.");
        public string LocalizedIcon => LocalizationManager.GetString("GAME_PROFILES_ICON", "Icon");
        public string LocalizedCreate => LocalizationManager.GetString("GAME_PROFILES_CREATE_BTN", "Create");
        public string LocalizedSave => LocalizationManager.GetString("GAME_PROFILES_SAVE_BTN", "Save");
        public string LocalizedDiscard => LocalizationManager.GetString("GAME_PROFILES_DISCARD_BTN", "Discard");
        public string LocalizedImportModpack => LocalizationManager.GetString("MODS_IMPORT_MRPACK_BTN", "Import mrpack file");
        public string LocalizedLanguage => LocalizationManager.GetString("LANGUAGE", "Language");
        public string LocalizedTheme => LocalizationManager.GetString("SETTINGS_CARD_THEME", "Theme");
        public string LocalizedDiscord => LocalizationManager.GetString("SETTINGS_CARD_DISCORD", "Discord Presence");
        public string LocalizedEnableDiscordRpc => LocalizationManager.GetString("SETTINGS_DISCORD_ENABLE", "Enable Discord Rich Presence");
        public string LocalizedAddMicrosoftAccount => LocalizationManager.GetString("SETTINGS_ACCOUNT_ADD_MS", "Add Microsoft Account");
        public string LocalizedAddOfflineAccount => LocalizationManager.GetString("SETTINGS_ACCOUNT_ADD_OFFLINE", "Add Offline Account");
        public string LocalizedRemove => LocalizationManager.GetString("REMOVE", "Remove");
        public string LocalizedPerformance => LocalizationManager.GetString("PERFORMANCE", "Performance Tier");
        public string LocalizedCheckForUpdates => LocalizationManager.GetString("SETTINGS_ABOUT_CHECK_UPDATES", "Check for Updates");
        public string LocalizedViewOnGitHub => LocalizationManager.GetString("SETTINGS_ABOUT_GITHUB", "View on GitHub");
        public string LocalizedResourcePacksTab => LocalizationManager.GetString("RES_SH_RP_TITLE", "Resource Packs");
        public string LocalizedShaderPacksTab => LocalizationManager.GetString("RES_SH_SP_TITLE", "Shader Packs");
        public string LocalizedModdingTab => LocalizationManager.GetString("MODS_TAB_TITLE", "Modding");
        public string LocalizedUpdate => LocalizationManager.GetString("UPDATE", "Update");
        public string LocalizedDownload => LocalizationManager.GetString("DOWNLOAD", "Download");
        public string LocalizedAdd => LocalizationManager.GetString("ADD", "Add");
        public string LocalizedServerManager => LocalizationManager.GetString("SERVER_MANAGER", "Server Manager");
        public string LocalizedGeneralSettings => LocalizationManager.GetString("SETTINGS_GENERAL_TITLE", "General Settings");
        public string LocalizedAccountManagement => LocalizationManager.GetString("SETTINGS_ACCOUNTS_TITLE", "Account Management");
        public string LocalizedAdvancedSettings => LocalizationManager.GetString("SETTINGS_ADVANCED_TITLE", "Advanced Settings");
        public string LocalizedLanguageDesc => LocalizationManager.GetString("SETTINGS_LANGUAGE_DESC", "Choose your preferred language for the launcher interface.");
        public string LocalizedLanguageNote => LocalizationManager.GetString("SETTINGS_LANGUAGE_NOTE", "Note: Changing language requires restarting the application.");
        public string LocalizedThemeDesc => LocalizationManager.GetString("SETTINGS_THEME_DESC", "Choose your preferred visual theme.");
        public string LocalizedThemeNote => LocalizationManager.GetString("SETTINGS_THEME_NOTE", "Theme changes apply immediately.");
        public string LocalizedMinecraftAccounts => LocalizationManager.GetString("SETTINGS_MC_ACCOUNTS", "Minecraft Accounts");
        public string LocalizedMinecraftAccountsDesc => LocalizationManager.GetString("SETTINGS_MC_ACCOUNTS_DESC", "Manage your Minecraft accounts for launching games.");
        public string LocalizedSkinPreview => LocalizationManager.GetString("SETTINGS_SKIN_PREVIEW", "Skin Preview");
        public string LocalizedSkinPreviewDesc => LocalizationManager.GetString("SETTINGS_SKIN_PREVIEW_DESC", "View and change your Minecraft skin. Select an account above to preview their skin.");
        public string LocalizedSelectAccountToView => LocalizationManager.GetString("SETTINGS_SELECT_ACCOUNT_VIEW", "Select an account to view skin");
        public string LocalizedUploadSkin => LocalizationManager.GetString("SETTINGS_UPLOAD_SKIN", "Upload Skin");
        public string LocalizedResetSkin => LocalizationManager.GetString("SETTINGS_RESET_SKIN", "Reset Skin");
        public string LocalizedModel => LocalizationManager.GetString("SETTINGS_MODEL", "Model:");
        public string LocalizedSteveClassic => LocalizationManager.GetString("SETTINGS_STEVE", "Steve (Classic)");
        public string LocalizedAlexSlim => LocalizationManager.GetString("SETTINGS_ALEX", "Alex (Slim)");
        public string LocalizedDiscordRichPresence => LocalizationManager.GetString("SETTINGS_DISCORD_TITLE", "Discord Rich Presence");
        public string LocalizedDiscordRpcDesc => LocalizationManager.GetString("SETTINGS_DISCORD_DESC", "Show your Minecraft activity in Discord.");
        public string LocalizedTelemetry => LocalizationManager.GetString("SETTINGS_TELEMETRY_TITLE", "Telemetry");
        public string LocalizedDeleteTelemetryFiles => LocalizationManager.GetString("SETTINGS_TELEMETRY_ENABLE", "Delete telemetry files on startup");
        public string LocalizedTelemetryDesc => LocalizationManager.GetString("SETTINGS_TELEMETRY_DESC", "Automatically clean up Minecraft telemetry data on launcher startup.");
        public string LocalizedPlugins => LocalizationManager.GetString("SETTINGS_PLUGINS_TITLE", "Plugins");
        public string LocalizedPluginsDesc => LocalizationManager.GetString("SETTINGS_PLUGINS_DESC", "Manage launcher plugins and extensions.");
        public string LocalizedAddPlugin => LocalizationManager.GetString("SETTINGS_ADD_PLUGIN", "Add Plugin");
        public string LocalizedRefresh => LocalizationManager.GetString("REFRESH", "Refresh");
        public string LocalizedDebugMode => LocalizationManager.GetString("SETTINGS_DEBUG_TITLE", "Debug Mode");
        public string LocalizedEnableDebugMode => LocalizationManager.GetString("SETTINGS_DEBUG_ENABLE", "Enable debug mode");
        public string LocalizedDebugModeDesc => LocalizationManager.GetString("SETTINGS_DEBUG_DESC", "Show detailed logging and debugging information.");
        public string LocalizedGpuSettings => LocalizationManager.GetString("SETTINGS_GPU_TITLE", "GPU Settings");
        public string LocalizedForceDiscreteGpu => LocalizationManager.GetString("SETTINGS_GPU_ENABLE", "Force use discrete GPU (NVIDIA/AMD)");
        public string LocalizedGpuDesc => LocalizationManager.GetString("SETTINGS_GPU_DESC", "Forces Minecraft to use your dedicated graphics card instead of integrated graphics. Improves performance on laptops with multiple GPUs.");
        public string LocalizedNewsBrowser => LocalizationManager.GetString("SETTINGS_BROWSER_TITLE", "News Browser");
        public string LocalizedNewsBrowserDesc => LocalizationManager.GetString("SETTINGS_BROWSER_DESC", "Choose the browser engine for displaying news.");
        public string LocalizedNewsBrowserNote => LocalizationManager.GetString("SETTINGS_BROWSER_NOTE", "WebView2 provides modern features, IE for compatibility.");
        public string LocalizedJavaInstallation => LocalizationManager.GetString("SETTINGS_JAVA_TITLE", "Java Installation");
        public string LocalizedJavaInstallationDesc => LocalizationManager.GetString("SETTINGS_JAVA_DESC", "Install and manage Java versions for Minecraft. Different versions require different Java runtimes.");
        public string LocalizedJavaVersion => LocalizationManager.GetString("GAME_PROFILES_JAVA", "Java Version");
        public string LocalizedSettingsSubtitle => LocalizationManager.GetString("SETTINGS_SUBTITLE", "Configure your launcher preferences");
        public string LocalizedAboutVersion => UpdateManager.GetAboutVersionText(
            LocalizationManager.GetString("SETTINGS_ABOUT_VERSION", "Version:"));
        public string LocalizedAboutDesc => LocalizationManager.GetString("SETTINGS_ABOUT_DESC", "A modern Legacy Minecraft launcher with advanced features.");
        public string LocalizedDevelopedBy => LocalizationManager.GetString("SETTINGS_ABOUT_DEVS", "Developed by: adasjusk and previously vakarux");
        public string LocalizedSearch => LocalizationManager.GetString("SEARCH", "Search:");
        public string LocalizedAddPack => LocalizationManager.GetString("ADD_PACK", "Add Pack");
        public string LocalizedAddShader => LocalizationManager.GetString("ADD_SHADER", "Add Shader");
        public string LocalizedAddMod => LocalizationManager.GetString("ADD_MOD", "Add Mod");
        public string LocalizedRemoveSelected => LocalizationManager.GetString("REMOVE_SELECTED", "Remove Selected");
        public string LocalizedImportMrpack => LocalizationManager.GetString("IMPORT_MRPACK", "Import .mrpack");
        public string LocalizedCancel => LocalizationManager.GetString("CANCEL", "Cancel");
        public string LocalizedAddServer => LocalizationManager.GetString("ADD_SERVER", "Add Server");
        public string LocalizedQuickPlay => LocalizationManager.GetString("QUICK_PLAY", "Quick Play");
        public string LocalizedHidden => LocalizationManager.GetString("HIDDEN", "Hidden");
        public string LocalizedManageAccounts => LocalizationManager.GetString("MANAGE_ACCOUNTS", "Manage Accounts");
        private readonly ObservableCollection<MinecraftInstance> _gameInstances = [];
        public ObservableCollection<MinecraftInstance> GameInstances => _gameInstances;
        private readonly ObservableCollection<UserProfile> _userProfiles = [];
        private readonly ObservableCollection<PluginInfo> _plugins = [];
        private List<ModInfo> _allMods = [];
        private List<ResourcePackInfo> _allResourcePacks = [];
        private List<ResourcePackInfo> _allShaderPacks = [];
        private DiscordRpcManager? _discordRpcManager;
        private bool _isEditingInstance = false;
        private string? _editingInstanceId;
        private string? _tempIconPath;
        private string? _editingVersion;
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected override void OnClosed(EventArgs e)
        {
            _discordRpcManager?.Dispose();
            base.OnClosed(e);
        }
        private void InitializeDiscordRpc()
        {
            try
            {
                if (DiscordRpcCheckBox.IsChecked == true)
                {
                    if (_discordRpcManager == null)
                    {
                        _discordRpcManager = new DiscordRpcManager();
                        _discordRpcManager.Initialize();
                    }
                    _discordRpcManager.UpdatePresence("Idling in launcher");
                }
                else
                {
                    if (_discordRpcManager != null)
                    {
                        _discordRpcManager.Dispose();
                        _discordRpcManager = null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Discord RPC error: {ex.Message}");
            }
        }
        private void UpdateDiscordRpc(string state, string details = "")
        {
            try
            {
                if (DiscordRpcCheckBox.IsChecked == true && _discordRpcManager != null)
                {
                    _discordRpcManager.UpdatePresence(state, details);
                }
            }
            catch { }
        }
        private void RefreshLocalizedStrings()
        {
            try
            {
                CopyLogText.Text = LocalizationManager.GetString("COPY_LOG", "Copy Log");
                ExportLogText.Text = LocalizationManager.GetString("EXPORT_LOG_MCLOGS", "Export to mclo.gs");
            }
            catch { }
            OnPropertyChanged(nameof(LocalizedUpdateNotes));
            OnPropertyChanged(nameof(LocalizedLauncherLog));
            OnPropertyChanged(nameof(LocalizedGameProfiles));
            OnPropertyChanged(nameof(LocalizedMods));
            OnPropertyChanged(nameof(LocalizedResourcePacks));
            OnPropertyChanged(nameof(LocalizedSettings));
            OnPropertyChanged(nameof(LocalizedProfile));
            OnPropertyChanged(nameof(LocalizedGameProfilesTitle));
            OnPropertyChanged(nameof(LocalizedPlay));
            OnPropertyChanged(nameof(LocalizedNewProfile));
            OnPropertyChanged(nameof(LocalizedGeneral));
            OnPropertyChanged(nameof(LocalizedAccounts));
            OnPropertyChanged(nameof(LocalizedAdvanced));
            OnPropertyChanged(nameof(LocalizedAbout));
            OnPropertyChanged(nameof(LocalizedClearLog));
            OnPropertyChanged(nameof(LocalizedSelect));
            OnPropertyChanged(nameof(LocalizedEdit));
            OnPropertyChanged(nameof(LocalizedDuplicate));
            OnPropertyChanged(nameof(LocalizedOpenFolder));
            OnPropertyChanged(nameof(LocalizedDelete));
            OnPropertyChanged(nameof(LocalizedInstanceSelect));
            OnPropertyChanged(nameof(LocalizedInstanceEdit));
            OnPropertyChanged(nameof(LocalizedInstanceDuplicate));
            OnPropertyChanged(nameof(LocalizedInstanceOpenFolder));
            OnPropertyChanged(nameof(LocalizedInstanceDelete));
            OnPropertyChanged(nameof(LocalizedCreateProfile));
            OnPropertyChanged(nameof(LocalizedEditProfile));
            OnPropertyChanged(nameof(LocalizedName));
            OnPropertyChanged(nameof(LocalizedVersion));
            OnPropertyChanged(nameof(LocalizedModLoader));
            OnPropertyChanged(nameof(LocalizedLoaderVersion));
            OnPropertyChanged(nameof(LocalizedRam));
            OnPropertyChanged(nameof(LocalizedJavaArgs));
            OnPropertyChanged(nameof(LocalizedJavaArgsDesc));
            OnPropertyChanged(nameof(LocalizedIcon));
            OnPropertyChanged(nameof(LocalizedCreate));
            OnPropertyChanged(nameof(LocalizedSave));
            OnPropertyChanged(nameof(LocalizedDiscard));
            OnPropertyChanged(nameof(LocalizedImportModpack));
            OnPropertyChanged(nameof(LocalizedLanguage));
            OnPropertyChanged(nameof(LocalizedTheme));
            OnPropertyChanged(nameof(LocalizedDiscord));
            OnPropertyChanged(nameof(LocalizedEnableDiscordRpc));
            OnPropertyChanged(nameof(LocalizedAddMicrosoftAccount));
            OnPropertyChanged(nameof(LocalizedAddOfflineAccount));
            OnPropertyChanged(nameof(LocalizedRemove));
            OnPropertyChanged(nameof(LocalizedPerformance));
            OnPropertyChanged(nameof(LocalizedCheckForUpdates));
            OnPropertyChanged(nameof(LocalizedViewOnGitHub));
            OnPropertyChanged(nameof(LocalizedResourcePacksTab));
            OnPropertyChanged(nameof(LocalizedShaderPacksTab));
            OnPropertyChanged(nameof(LocalizedModdingTab));
            OnPropertyChanged(nameof(LocalizedUpdate));
            OnPropertyChanged(nameof(LocalizedDownload));
            OnPropertyChanged(nameof(LocalizedAdd));
            OnPropertyChanged(nameof(LocalizedServerManager));
            OnPropertyChanged(nameof(LocalizedGeneralSettings));
            OnPropertyChanged(nameof(LocalizedAccountManagement));
            OnPropertyChanged(nameof(LocalizedAdvancedSettings));
            OnPropertyChanged(nameof(LocalizedLanguageDesc));
            OnPropertyChanged(nameof(LocalizedLanguageNote));
            OnPropertyChanged(nameof(LocalizedThemeDesc));
            OnPropertyChanged(nameof(LocalizedThemeNote));
            OnPropertyChanged(nameof(LocalizedMinecraftAccounts));
            OnPropertyChanged(nameof(LocalizedMinecraftAccountsDesc));
            OnPropertyChanged(nameof(LocalizedSkinPreview));
            OnPropertyChanged(nameof(LocalizedSkinPreviewDesc));
            OnPropertyChanged(nameof(LocalizedSelectAccountToView));
            OnPropertyChanged(nameof(LocalizedUploadSkin));
            OnPropertyChanged(nameof(LocalizedResetSkin));
            OnPropertyChanged(nameof(LocalizedModel));
            OnPropertyChanged(nameof(LocalizedSteveClassic));
            OnPropertyChanged(nameof(LocalizedAlexSlim));
            OnPropertyChanged(nameof(LocalizedDiscordRichPresence));
            OnPropertyChanged(nameof(LocalizedDiscordRpcDesc));
            OnPropertyChanged(nameof(LocalizedTelemetry));
            OnPropertyChanged(nameof(LocalizedDeleteTelemetryFiles));
            OnPropertyChanged(nameof(LocalizedTelemetryDesc));
            OnPropertyChanged(nameof(LocalizedPlugins));
            OnPropertyChanged(nameof(LocalizedPluginsDesc));
            OnPropertyChanged(nameof(LocalizedAddPlugin));
            OnPropertyChanged(nameof(LocalizedRefresh));
            OnPropertyChanged(nameof(LocalizedDebugMode));
            OnPropertyChanged(nameof(LocalizedEnableDebugMode));
            OnPropertyChanged(nameof(LocalizedDebugModeDesc));
            OnPropertyChanged(nameof(LocalizedGpuSettings));
            OnPropertyChanged(nameof(LocalizedForceDiscreteGpu));
            OnPropertyChanged(nameof(LocalizedGpuDesc));
            OnPropertyChanged(nameof(LocalizedNewsBrowser));
            OnPropertyChanged(nameof(LocalizedNewsBrowserDesc));
            OnPropertyChanged(nameof(LocalizedNewsBrowserNote));
            OnPropertyChanged(nameof(LocalizedJavaInstallation));
            OnPropertyChanged(nameof(LocalizedJavaInstallationDesc));
            OnPropertyChanged(nameof(LocalizedJavaVersion));
            OnPropertyChanged(nameof(LocalizedSettingsSubtitle));
            OnPropertyChanged(nameof(LocalizedAboutVersion));
            OnPropertyChanged(nameof(LocalizedAboutDesc));
            OnPropertyChanged(nameof(LocalizedDevelopedBy));
            OnPropertyChanged(nameof(LocalizedSearch));
            OnPropertyChanged(nameof(LocalizedAddPack));
            OnPropertyChanged(nameof(LocalizedAddShader));
            OnPropertyChanged(nameof(LocalizedAddMod));
            OnPropertyChanged(nameof(LocalizedRemoveSelected));
            OnPropertyChanged(nameof(LocalizedImportMrpack));
            OnPropertyChanged(nameof(LocalizedCancel));
            OnPropertyChanged(nameof(LocalizedAddServer));
            OnPropertyChanged(nameof(LocalizedQuickPlay));
            OnPropertyChanged(nameof(LocalizedHidden));
            OnPropertyChanged(nameof(LocalizedManageAccounts));
        }
        public MainWindow()
        {
            try
            {
                InitializeComponent();
                WebView2Helper.Prepare(NewsWebView);
                DataContext = this;
                Loaded += MainWindow_Loaded;
            Loaded += (s, e) => ApplyNewUiLocalization();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during window initialization: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try {
                InitializeApplication();
                try
                {
                    string iconName = "orange.ico";
                    string[] checkPaths =
                    [
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "oranglaunch", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Other", "images", iconName)
                    ];
                    foreach (var path in checkPaths)
                    {
                        var fullPath = System.IO.Path.GetFullPath(path);
                        if (File.Exists(fullPath))
                        {
                            this.Icon = new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Failed to load window icon: {ex.Message}");
                }
                var interopHelper = new System.Windows.Interop.WindowInteropHelper(this);
                WindowStyleManager.EnableBlur(interopHelper.Handle);
                WindowStyleManager.EnableClassicTheme(interopHelper.Handle);
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { WelcomeWizard.ShowIfNeeded(this); }
                    catch (Exception wex) { LogMessage($"Welcome wizard failed: {wex.Message}"); }
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during window load: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void InitializeApplication()
        {
            try
            {
                LogMessage("Initializing application...");
                LocalizationManager.LoadLanguage("en-US");
                LoadProfiles();
                ClearLogButton.Click += ClearLogButton_Click;
                ProfileComboBox.SelectionChanged += ProfileComboBox_SelectionChanged;
                GameProfileComboBox.SelectionChanged += GameProfileComboBox_SelectionChanged;
                MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;
                LoadGameProfiles();
                LoadNews();
                LoadSettings();
                LoadGameProfilesList();
                InitializeSettings();
                ApplyInitialTheme();
                await InitializeNewsBrowser();
                ApplyNewsBrowserFromSettings();
                LoadResourcePacks();
                LoadShaderPacks();
                LoadMods();
                LoadServerList();
                if (StartupState.PendingMrpackPath is string mrpack)
                {
                    StartupState.PendingMrpackPath = null;
                    await ImportMrpackFileAsync(mrpack);
                }
                // repair profiles whose mod loader was never really installed.
                try
                {
                    var repaired = await InstanceRepairer.RepairAllAsync(msg => LogMessage(msg));
                    if (repaired > 0)
                    {
                        LogMessage($"Repaired {repaired} profile(s) with missing mod loaders.");
                        LoadGameProfilesList();
                        LoadGameProfiles();
                    }
                }
                catch (Exception rex) { LogMessage($"Profile repair failed: {rex.Message}"); }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during initialization: {ex.Message}");
                MessageBox.Show($"Error during initialization: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            return System.IO.Path.Combine(PlatformPaths.GetMinecraftDir(), "mods");
        }
        private string GetCurrentResourcePacksPath(bool isShader)
        {
            string subDir = isShader ? "shaderpacks" : "resourcepacks";
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance) return isShader ? instance.ShaderPacksDir : instance.ResourcePacksDir;
                else if (item.Tag is GameProfile profile) return System.IO.Path.Combine(profile.GameDir ?? PlatformPaths.GetMinecraftDir(), subDir);
            }
            return System.IO.Path.Combine(PlatformPaths.GetMinecraftDir(), subDir);
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
            if (string.IsNullOrWhiteSpace(filter))
                ModsListBox.ItemsSource = new ObservableCollection<ModInfo>(_allMods);
            else
                ModsListBox.ItemsSource = new ObservableCollection<ModInfo>(_allMods.Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ApplyResourcePackFilter()
        {
            var filter = ResourcePacksSearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(filter))
                ResourcePacksListBox.ItemsSource = new ObservableCollection<ResourcePackInfo>(_allResourcePacks);
            else
                ResourcePacksListBox.ItemsSource = new ObservableCollection<ResourcePackInfo>(_allResourcePacks.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ApplyShaderPackFilter()
        {
            var filter = ShaderPacksSearchTextBox.Text;
            if (string.IsNullOrWhiteSpace(filter))
                ShaderPacksListBox.ItemsSource = new ObservableCollection<ResourcePackInfo>(_allShaderPacks);
            else
                ShaderPacksListBox.ItemsSource = new ObservableCollection<ResourcePackInfo>(_allShaderPacks.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        }
        private void ModsSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyModFilter();
        private void ResourcePacksSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyResourcePackFilter();
        private void ShaderPacksSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyShaderPackFilter();
        private void AddModButton_Click(object sender, RoutedEventArgs e)
        {
            var path = GetCurrentModsPath();
            ModManager.Instance.AddMods(path);
            LoadMods();
        }
        private void RemoveModButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModsListBox.SelectedItems.Cast<ModInfo>().ToList();
            if (selectedMods.Count > 0) { ModManager.Instance.RemoveMods(selectedMods); LoadMods(); }
        }
        private void OpenModsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var path = GetCurrentModsPath();
            ModManager.Instance.OpenModsFolder(path);
        }
        private void RefreshModsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMods();
        }
        private void AddResourcePackButton_Click(object sender, RoutedEventArgs e)
        {
            var path = GetCurrentResourcePacksPath(false);
            ResourceManager.Instance.AddPacks(path, false);
            LoadResourcePacks();
        }
        private void RefreshResourcePacksButton_Click(object sender, RoutedEventArgs e)
        {
            LoadResourcePacks();
        }
        private void RemoveResourcePackButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPacks = ResourcePacksListBox.SelectedItems.Cast<ResourcePackInfo>().ToList();
            if (selectedPacks.Count > 0) { ResourceManager.Instance.RemovePacks(selectedPacks); LoadResourcePacks(); }
        }
        private void OpenResourcePacksButton_Click(object sender, RoutedEventArgs e)
        {
            var path = GetCurrentResourcePacksPath(false);
            ResourceManager.Instance.OpenFolder(path);
        }
        private void AddShaderPackButton_Click(object sender, RoutedEventArgs e)
        {
            var path = GetCurrentResourcePacksPath(true);
            ResourceManager.Instance.AddPacks(path, true);
            LoadShaderPacks();
        }
        private void RefreshShaderPacksButton_Click(object sender, RoutedEventArgs e)
        {
            LoadShaderPacks();
        }
        private void RemoveShaderPackButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPacks = ShaderPacksListBox.SelectedItems.Cast<ResourcePackInfo>().ToList();
            if (selectedPacks.Count > 0) { ResourceManager.Instance.RemovePacks(selectedPacks); LoadShaderPacks(); }
        }
        private void RefreshServerButton_Click(object sender, RoutedEventArgs e)
        {
            LoadServerList();
        }
        private void OpenShaderPacksButton_Click(object sender, RoutedEventArgs e)
        {
            var path = GetCurrentResourcePacksPath(true);
            ResourceManager.Instance.OpenFolder(path);
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
                        {
                            var vm = new ServerViewModel(server)
                            {
                                SupportsQuickPlay = supportsQuickPlay
                            };
                            viewModels.Add(vm);
                        }
                        ServersListBox.ItemsSource = viewModels;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading servers: {ex.Message}");
                MessageBox.Show($"Failed to load server list: {ex.Message}");
            }
        }
        private bool SupportsQuickPlay(string version)
        {
            try
            {
                var parts = version.Split('.');
                if (parts.Length >= 2)
                {
                    if (int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                    {
                        if (major > 1) return true;
                        if (major == 1 && minor > 20) return true;
                        if (major == 1 && minor == 20)
                        {
                            if (parts.Length >= 3 && int.TryParse(parts[2], out int patch))
                            {
                                return patch >= 3;
                            }
                            return false;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        private async void QuickPlayServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is ServerViewModel viewModel)
                {
                    var server = viewModel.Server;
                    if (ProfileComboBox.SelectedItem == null || (ProfileComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content?.ToString() == "Demo User"))
                    {
                        ManageAccountsButton_Click(null, null);
                        MessageBox.Show("Please add or select an account before playing.", "Account Required", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    if (!(GameProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is MinecraftInstance instance))
                    {
                        MessageBox.Show("Please select a game instance first.", "Instance Required", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    StatusText = $"Quick Playing {server.Name}...";
                    LogMessage($"Quick Play: Connecting to {server.Name} ({server.Ip})...");
                    string version = !string.IsNullOrEmpty(instance.InstalledVersionName)
                        ? instance.InstalledVersionName!
                        : instance.Version;
                    string ramStr = instance.Ram;
                    string javaArgs = instance.JavaArgs;
                    string modLoader = instance.ModLoader;
                    string? gameDir = instance.MinecraftDir;
                    string? instanceJavaPath = instance.JavaPath;
                    int maxRam = 4096;
                    if (!string.IsNullOrEmpty(ramStr))
                    {
                        string digits = new(ramStr.Where(char.IsDigit).ToArray());
                        if (int.TryParse(digits, out int val)) { if (val < 64) maxRam = val * 1024; else maxRam = val; }
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
                            {
                                isTokenValid = await authManager.ValidateMinecraftToken(tokenToUse);
                            }
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
                                    LogMessage("Session refreshed successfully.");
                                }
                                catch (Exception ex)
                                {
                                    LogMessage($"Refresh failed: {ex.Message}");
                                }
                            }
                            if (isTokenValid)
                            {
                                session = new MSession(userProfile.Username, tokenToUse, userProfile.Uuid);
                            }
                            else
                            {
                                LogMessage("Session invalid. Launching as Offline.");
                                session = MSession.CreateOfflineSession(userProfile.Username);
                            }
                        }
                        else
                        {
                            session = MSession.CreateOfflineSession(userProfile.Username);
                        }
                    }
                    UpdateDiscordRpc("Playing Minecraft", $"{version} on {server.Name}");
                    string serverAddress = server.Ip.Split(':')[0];
                    int port = 25565;
                    if (server.Ip.Contains(':'))
                    {
                        int.TryParse(server.Ip.Split(':')[1], out port);
                    }
                    string performanceTier = instance.PerformanceTier ?? "Auto";
                    bool useDiscreteGpu = UseDiscreteGpuCheckBox.IsChecked ?? false;
                    _currentProcess = await GameProfileManager.Instance.LaunchGame(version, maxRam, javaArgs, session, modLoader, gameDir, serverAddress, port, performanceTier, useDiscreteGpu, instanceJavaPath);
                    LogMessage("===== QUICK PLAY LAUNCH =====");
                    LogMessage($"Server: {server.Name} ({server.Ip})");
                    LogMessage($"Executable: {_currentProcess.StartInfo.FileName}");
                    LogMessage($"Working Directory: {_currentProcess.StartInfo.WorkingDirectory}");
                    LogMessage($"Arguments: {_currentProcess.StartInfo.Arguments}");
                    LogMessage("=============================");
                    _currentProcess.StartInfo.UseShellExecute = false;
                    _currentProcess.StartInfo.RedirectStandardOutput = true;
                    _currentProcess.StartInfo.RedirectStandardError = true;
                    _currentProcess.StartInfo.CreateNoWindow = false;
                    _currentProcess.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Dispatcher.Invoke(() => LogMessage($"[GAME] {e.Data}"));
                    };
                    _currentProcess.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Dispatcher.Invoke(() => LogMessage($"[GAME ERROR] {e.Data}"));
                    };
                    LogMessage($"Starting Minecraft and connecting to {server.Name}...");
                    _currentProcess.Start();
                    _currentProcess.BeginOutputReadLine();
                    _currentProcess.BeginErrorReadLine();
                    StatusText = $"Connected to {server.Name}";
                    PlayButton.Content = "STOP";
                    PlayButton.IsEnabled = true;
                    _ = Task.Run(async () =>
                    {
                        await _currentProcess.WaitForExitAsync();
                        Dispatcher.Invoke(() =>
                        {
                            StatusText = "Game exited";
                            PlayButton.Content = "PLAY";
                            LogMessage("Game exited: " + _currentProcess.ExitCode);
                            _currentProcess = null;
                            UpdateDiscordRpc("Idling in launcher");
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Quick Play failed: {ex.Message}]");
                MessageBox.Show($"Failed to quick play:\n{ex.Message}", "Quick Play Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "Quick Play failed";
            }
        }
        private void AddServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GameProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is MinecraftInstance instance)
                {
                    var nameDlg = new AddTextDialog("Server Name:", "Add Server", "Minecraft Server") { Owner = this };
                    if (nameDlg.ShowDialog() != true)
                    {
                        LogMessage("Server add cancelled (no name provided)");
                        return;
                    }
                    var name = nameDlg.ResultText ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        LogMessage("Server add cancelled (empty name)");
                        return;
                    }
                    var ipDlg = new AddTextDialog("Server Address:", "Add Server", "mc.hypixel.net") { Owner = this };
                    if (ipDlg.ShowDialog() != true)
                    {
                        LogMessage("Server add cancelled (no IP provided)");
                        return;
                    }
                    var ip = ipDlg.ResultText ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(ip))
                    {
                        LogMessage("Server add cancelled (empty IP)");
                        return;
                    }
                    var path = instance.MinecraftDir;
                    LogMessage($"Adding server to: {path}");
                    Directory.CreateDirectory(path);
                    var servers = ServerListManager.Instance.LoadServers(path);
                    LogMessage($"Loaded {servers.Count} existing servers");
                    servers.Add(new ServerInfo { Name = name, Ip = ip });
                    LogMessage($"Added server '{name}' ({ip}) to list");
                    ServerListManager.Instance.SaveServers(path, servers);
                    var savedPath = System.IO.Path.Combine(path, "servers.dat");
                    if (File.Exists(savedPath))
                    {
                        LogMessage($"SUCCESS: Server saved to {savedPath} (size: {new FileInfo(savedPath).Length} bytes)");
                        MessageBox.Show($"Server '{name}' added successfully!\n\nSaved to: {savedPath}", "Server Added", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        LogMessage($"ERROR: servers.dat file was not created at {savedPath}");
                        MessageBox.Show($"Server may not have been saved!\n\nExpected location: {savedPath}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    LoadServerList();
                }
                else
                {
                    MessageBox.Show("Please select a Game Instance first (Profiles do not manage servers directly).");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding server: {ex.Message}");
            }
        }
        private void RemoveServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServersListBox.SelectedItem is ServerViewModel viewModel && GameProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is MinecraftInstance instance)
            {
                var server = viewModel.Server;
                var path = instance.MinecraftDir;
                var servers = ServerListManager.Instance.LoadServers(path);
                var toRemove = servers.FirstOrDefault(s => s.Name == server.Name && s.Ip == server.Ip);
                if (toRemove != null)
                {
                    servers.Remove(toRemove);
                    ServerListManager.Instance.SaveServers(path, servers);
                    LoadServerList();
                }
            }
        }
        private void ManageAccountsButton_Click(object? sender, RoutedEventArgs? e)
        {
            MainTabControl.SelectedItem = SettingsTabItem;
            ShowSettingsPanel("Accounts");
        }
        private void InstanceContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
        private void SelectInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance)
            {
                InstanceManager.Instance.SetSelectedInstance(instance.InstanceId);
                LoadGameProfilesList();
                LoadGameProfiles();
                for (int i = 0; i < GameProfileComboBox.Items.Count; i++)
                {
                    if (GameProfileComboBox.Items[i] is ComboBoxItem cbi && cbi.Tag is MinecraftInstance inst && inst.InstanceId == instance.InstanceId)
                    {
                        GameProfileComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        private void OpenInstanceFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance)
            {
                string path = instance.BasePath;
                if (Directory.Exists(path)) Process.Start("explorer.exe", path);
            }
        }
        private void DeleteInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance)
            {
                var result = MessageBox.Show($"Are you sure you want to delete profile '{instance.Name}'? This cannot be undone.", "Delete Profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    if (InstanceManager.Instance.RemoveInstance(instance.InstanceId))
                    {
                        LogMessage("Profile deleted.");
                        LoadGameProfilesList();
                        LoadGameProfiles();
                    }
                }
            }
        }
        private void DuplicateInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is MinecraftInstance instance)
            {
                try
                {
                    string baseName = instance.Name;
                    string newName = $"{baseName} copy";
                    int counter = 2;
                    while (InstanceManager.Instance.GetInstanceByName(newName) != null)
                    {
                        newName = $"{baseName} copy #{counter}";
                        counter++;
                    }
                    var newInstance = InstanceManager.Instance.CreateInstance(newName, instance.Version, instance.ModLoader, instance.Ram);
                    if (newInstance != null)
                    {
                        newInstance.Icon = instance.Icon;
                        string sourcePath = instance.BasePath;
                        string destPath = newInstance.BasePath;
                        if (Directory.Exists(sourcePath))
                        {
                            string sourceModsFolder = System.IO.Path.Combine(sourcePath, "mods");
                            string destModsFolder = System.IO.Path.Combine(destPath, "mods");
                            if (Directory.Exists(sourceModsFolder))
                            {
                                Directory.CreateDirectory(destModsFolder);
                                foreach (string file in Directory.GetFiles(sourceModsFolder))
                                {
                                    string destFile = System.IO.Path.Combine(destModsFolder, System.IO.Path.GetFileName(file));
                                    File.Copy(file, destFile, true);
                                }
                            }
                            string sourceResFolder = System.IO.Path.Combine(sourcePath, "resourcepacks");
                            string destResFolder = System.IO.Path.Combine(destPath, "resourcepacks");
                            if (Directory.Exists(sourceResFolder))
                            {
                                Directory.CreateDirectory(destResFolder);
                                foreach (string file in Directory.GetFiles(sourceResFolder))
                                {
                                    string destFile = System.IO.Path.Combine(destResFolder, System.IO.Path.GetFileName(file));
                                    File.Copy(file, destFile, true);
                                }
                            }
                            string sourceShaderFolder = System.IO.Path.Combine(sourcePath, "shaderpacks");
                            string destShaderFolder = System.IO.Path.Combine(destPath, "shaderpacks");
                            if (Directory.Exists(sourceShaderFolder))
                            {
                                Directory.CreateDirectory(destShaderFolder);
                                foreach (string file in Directory.GetFiles(sourceShaderFolder))
                                {
                                    string destFile = System.IO.Path.Combine(destShaderFolder, System.IO.Path.GetFileName(file));
                                    File.Copy(file, destFile, true);
                                }
                            }
                            string sourceSavesFolder = System.IO.Path.Combine(sourcePath, "saves");
                            string destSavesFolder = System.IO.Path.Combine(destPath, "saves");
                            if (Directory.Exists(sourceSavesFolder))
                            {
                                CopyDirectory(sourceSavesFolder, destSavesFolder);
                            }
                            string sourceOptions = System.IO.Path.Combine(sourcePath, "options.txt");
                            string destOptions = System.IO.Path.Combine(destPath, "options.txt");
                            if (File.Exists(sourceOptions))
                            {
                                File.Copy(sourceOptions, destOptions, true);
                            }
                        }
                        InstanceManager.Instance.SaveInstances();
                        LogMessage($"Duplicated profile to '{newName}'");
                        LoadGameProfilesList();
                        LoadGameProfiles();
                        MessageBox.Show($"Profile duplicated as '{newName}'", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error duplicating profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"Duplication error: {ex.Message}");
                }
            }
        }
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }
        private void InstanceIconButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Icon",
                Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All Files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                _tempIconPath = openFileDialog.FileName;
                LogMessage($"Icon selected: {_tempIconPath}");
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(_tempIconPath, UriKind.Absolute);
                    bitmap.EndInit();
                    InstanceIconImage.Source = bitmap;
                }
                catch
                {
                }
            }
        }
        private void LoadInstanceIcon(MinecraftInstance? instance)
            => LoadLoaderIcon(instance?.ModLoader, instance?.Icon);
        private void UpdateEditViewIcon()
        {
            var loader = (ProfileModLoaderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            LoadLoaderIcon(loader, _tempIconPath);
        }
        private void LoadLoaderIcon(string? modLoader, string? customIconPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(customIconPath) && File.Exists(customIconPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(customIconPath, UriKind.Absolute);
                    bitmap.EndInit();
                    InstanceIconImage.Source = bitmap;
                    return;
                }
                string loaderLower = modLoader?.Trim().ToLowerInvariant() ?? "";
                bool isModded = loaderLower is not ("" or "vanilla" or "none");
                string[] iconNames = isModded
                    ? [System.IO.Path.Combine("loaders", $"{loaderLower}.png"), "minecraft-blue.png"]
                    : ["minecraft-green.png"];
                foreach (var iconName in iconNames)
                {
                    string[] checkPaths =
                    [
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Other", "images", iconName),
                        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Other", "images", iconName),
                    ];
                    foreach (var path in checkPaths)
                    {
                        var fullPath = System.IO.Path.GetFullPath(path);
                        if (File.Exists(fullPath))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                            bitmap.EndInit();
                            InstanceIconImage.Source = bitmap;
                            return;
                        }
                    }
                }
                InstanceIconImage.Source = null;
            }
            catch
            {
                InstanceIconImage.Source = null;
            }
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
        private void RemoveAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserProfile profile)
            {
                if (MessageBox.Show($"Remove account '{profile.Username}'?", "Remove Account", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    ProfileManager.Instance.RemoveProfile(profile.Uuid);
                    LoadAccountsData();
                    LoadProfiles();
                }
            }
        }
        private void LoadProfiles()
        {
            try
            {
                ProfileComboBox.Items.Clear();
                var profiles = ProfileManager.Instance.GetProfiles();
                foreach (var profile in profiles) ProfileComboBox.Items.Add(new ComboBoxItem { Content = profile.GetDisplayName(), Tag = profile });
                if (ProfileComboBox.Items.Count > 0)
                {
                    var selectedProfile = ProfileManager.Instance.GetSelectedProfile();
                    if (selectedProfile != null)
                    {
                        for (int i = 0; i < ProfileComboBox.Items.Count; i++)
                        {
                            if (ProfileComboBox.Items[i] is ComboBoxItem item && item.Tag is UserProfile profile && profile.Uuid == selectedProfile.Uuid)
                            {
                                ProfileComboBox.SelectedIndex = i; break;
                            }
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
                foreach (var instance in instances) GameProfileComboBox.Items.Add(new ComboBoxItem { Content = instance.Name, Tag = instance });
                var profiles = GameProfileManager.Instance.GetProfiles();
                foreach (var profile in profiles) { if (!instances.Any(i => i.Name == profile.Name)) GameProfileComboBox.Items.Add(new ComboBoxItem { Content = profile.Name, Tag = profile }); }
                if (GameProfileComboBox.Items.Count > 0)
                {
                    var selectedInstance = InstanceManager.Instance.GetSelectedInstance();
                    var selectedProfile = GameProfileManager.Instance.GetSelectedProfile();
                    if (selectedInstance != null)
                    {
                        for (int i = 0; i < GameProfileComboBox.Items.Count; i++) { if (GameProfileComboBox.Items[i] is ComboBoxItem item && item.Tag is MinecraftInstance instance && instance.InstanceId == selectedInstance.InstanceId) { GameProfileComboBox.SelectedIndex = i; break; } }
                    }
                    else if (selectedProfile != null)
                    {
                        for (int i = 0; i < GameProfileComboBox.Items.Count; i++) { if (GameProfileComboBox.Items[i] is ComboBoxItem item && item.Tag is GameProfile profile && profile.Id == selectedProfile.Id) { GameProfileComboBox.SelectedIndex = i; break; } }
                    }
                    else GameProfileComboBox.SelectedIndex = 0;
                    UpdateGameProfileInfo();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.Message}");
                UpdateGameProfileInfo();
            }
        }
        private void LoadNews() { LogMessage("Loading news..."); }
        private void LoadSettings() { LogMessage("Loading settings..."); }
        private void UpdateGameProfileInfo()
        {
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance)
                {
                    GameProfileInfo = $"Instance: {instance.Name} | {instance.Version} ({instance.ModLoader})";
                    UpdateModdingTabState(instance.ModLoader);
                }
                else if (item.Tag is GameProfile profile)
                {
                    GameProfileInfo = $"Profile: {profile.Name} | {profile.Version} ({profile.ModLoader})";
                    UpdateModdingTabState(profile.ModLoader);
                }
                else
                {
                    GameProfileInfo = "No profile selected";
                    UpdateModdingTabState("vanilla");
                }
            }
        }
        private void UpdateModdingTabState(string? modLoader)
        {
            bool shouldDisable = string.IsNullOrEmpty(modLoader) ||
                                modLoader.Equals("vanilla", StringComparison.OrdinalIgnoreCase) ||
                                modLoader.Equals("none", StringComparison.OrdinalIgnoreCase);
            if (ModdingTabItem != null)
            {
                ModdingTabItem.IsEnabled = !shouldDisable;
                ModdingTabItem.Opacity = shouldDisable ? 0.5 : 1.0;
                if (shouldDisable && ResourcePacksTabControl?.SelectedItem == ModdingTabItem)
                {
                    ResourcePacksTabControl.SelectedIndex = 0;
                }
            }
        }
        private Process? _currentProcess;
        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayButton.Content.ToString() == "STOP")
            {
                try
                {
                    _currentProcess?.Kill();
                    StatusText = "Game killed";
                    PlayButton.Content = "PLAY";
                    PlayButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    LogMessage("Failed to stop: " + ex.Message);
                }
                return;
            }
            if (ProfileComboBox.SelectedItem == null || (ProfileComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content?.ToString() == "Demo User"))
            {
                ManageAccountsButton_Click(null, null);
                MessageBox.Show("Please add or select an account before playing.", "Account Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            StatusText = "Initializing Launch...";
            PlayButton.IsEnabled = false;
            try
            {
                string version = "26.2";
                string ramStr = "2096";
                string javaArgs = "";
                string modLoader = "None";
                string? gameDir = null;
                string performanceTier = "Auto";
                string? instanceJavaPath = null;
                if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
                {
                    if (item.Tag is MinecraftInstance instance)
                    {
                        version = !string.IsNullOrEmpty(instance.InstalledVersionName)
                            ? instance.InstalledVersionName!
                            : instance.Version;
                        ramStr = instance.Ram;
                        javaArgs = instance.JavaArgs;
                        modLoader = instance.ModLoader;
                        gameDir = instance.MinecraftDir;
                        performanceTier = instance.PerformanceTier ?? "Auto";
                        instanceJavaPath = instance.JavaPath;
                    }
                    else if (item.Tag is GameProfile profile)
                    {
                        version = profile.Version;
                        ramStr = profile.Ram;
                        modLoader = profile.ModLoader;
                        gameDir = profile.GameDir;
                    }
                }
                int maxRam = 4096;
                if (!string.IsNullOrEmpty(ramStr))
                {
                    string digits = new(ramStr.Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out int val)) { if (val < 64) maxRam = val * 1024; else maxRam = val; }
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
                        {
                            isTokenValid = await authManager.ValidateMinecraftToken(tokenToUse);
                        }
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
                                LogMessage("Session refreshed successfully.");
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"Refresh failed: {ex.Message}");
                            }
                        }
                        if (isTokenValid)
                        {
                            session = new MSession(userProfile.Username, tokenToUse, userProfile.Uuid);
                        }
                        else
                        {
                            LogMessage("Session invalid. Launching as Offline.");
                            session = MSession.CreateOfflineSession(userProfile.Username);
                        }
                    }
                    else
                    {
                        session = MSession.CreateOfflineSession(userProfile.Username);
                    }
                }
                if (DeleteTelemetryCheckBox.IsChecked == true)
                {
                    try
                    {
                        string? targetDir = gameDir;
                        if (string.IsNullOrEmpty(targetDir) && GameProfileComboBox.SelectedItem is ComboBoxItem telemetryItem && telemetryItem.Tag is MinecraftInstance inst)
                        {
                            targetDir = inst.MinecraftDir;
                        }
                        if (string.IsNullOrEmpty(targetDir))
                        {
                            targetDir = PlatformPaths.GetMinecraftDir();
                        }
                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            string telemetryPath = System.IO.Path.Combine(targetDir, "logs", "telemetry");
                            if (Directory.Exists(telemetryPath))
                            {
                                LogMessage($"Deleting telemetry at: {telemetryPath}");
                                Directory.Delete(telemetryPath, true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to delete telemetry: {ex.Message}");
                    }
                }
                LogMessage($"Preparing to launch version: {version} ({modLoader})");
                StatusText = "Checking game files...";
                UpdateDiscordRpc("Playing Minecraft", $"{version} ({modLoader})");
                bool useDiscreteGpu = UseDiscreteGpuCheckBox.IsChecked ?? false;
                _currentProcess = await GameProfileManager.Instance.LaunchGame(version, maxRam, javaArgs, session, modLoader, gameDir, null, null, performanceTier, useDiscreteGpu, instanceJavaPath);
                LogMessage("===== LAUNCH COMMAND =====");
                LogMessage($"Executable: {_currentProcess.StartInfo.FileName}");
                LogMessage($"Working Directory: {_currentProcess.StartInfo.WorkingDirectory}");
                LogMessage($"Arguments: {_currentProcess.StartInfo.Arguments}");
                LogMessage("==========================");
                _currentProcess.StartInfo.UseShellExecute = false;
                _currentProcess.StartInfo.RedirectStandardOutput = true;
                _currentProcess.StartInfo.RedirectStandardError = true;
                _currentProcess.StartInfo.CreateNoWindow = false;
                _currentProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Dispatcher.Invoke(() => LogMessage($"[GAME] {e.Data}"));
                };
                _currentProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Dispatcher.Invoke(() => LogMessage($"[GAME ERROR] {e.Data}"));
                };
                LogMessage("Starting Minecraft...");
                _currentProcess.Start();
                _currentProcess.BeginOutputReadLine();
                _currentProcess.BeginErrorReadLine();
                StatusText = "Minecraft Running";
                PlayButton.Content = "STOP";
                PlayButton.IsEnabled = true;
                _ = Task.Run(async () =>
                {
                    await _currentProcess.WaitForExitAsync();
                    Dispatcher.Invoke(() => {
                        StatusText = "Game exited";
                        PlayButton.Content = "PLAY";
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
                MessageBox.Show($"Failed to launch:\n{ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                PlayButton.IsEnabled = true; PlayButton.Content = "PLAY";
                UpdateDiscordRpc("Idling in launcher");
            }
        }
        private void ClearLogButton_Click(object sender, RoutedEventArgs e) => LogTextBox.Clear();
        private void CopyLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(LogTextBox.Text ?? "");
                StatusText = LocalizationManager.GetString("LOG_COPIED", "Log copied to clipboard.");
            }
            catch (Exception ex) { LogMessage($"Copy log failed: {ex.Message}"); }
        }
        private async void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportLogButton.IsEnabled = false;
                StatusText = LocalizationManager.GetString("LOG_UPLOADING", "Uploading log to mclo.gs...");
                var (success, urlOrError) = await MclogsClient.UploadAsync(LogTextBox.Text ?? "");
                if (success)
                {
                    try { Clipboard.SetText(urlOrError); } catch { }
                    StatusText = $"{LocalizationManager.GetString("LOG_UPLOADED", "Log uploaded (link copied):")} {urlOrError}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = urlOrError, UseShellExecute = true });
                }
                else
                {
                    StatusText = $"{LocalizationManager.GetString("LOG_UPLOAD_FAILED", "Log upload failed:")} {urlOrError}";
                }
            }
            catch (Exception ex) { LogMessage($"Export log failed: {ex.Message}"); }
            finally { ExportLogButton.IsEnabled = true; }
        }
        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile profile) { ProfileManager.Instance.SetSelectedProfile(profile.Uuid); StatusText = $"Welcome {profile.Username}!"; } }
        private void ProfileVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // mc version drives which loader versions exist
            if (ProfileModLoaderComboBox.SelectedItem == null)
                ProfileModLoaderComboBox.SelectedIndex = 0; // fires the cascade itself
            else
                ProfileModLoaderComboBox_SelectionChanged(ProfileModLoaderComboBox, e);
        }
        private async void ProfileModLoaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileModLoaderComboBox.SelectedItem is ComboBoxItem selected)
            {
                var loaderType = selected.Tag?.ToString()?.ToLower() ?? "";
                UpdateEditViewIcon();
                var mcVersion = ProfileVersionComboBox.SelectedItem?.ToString() ?? ProfileVersionComboBox.Text;
                if (string.IsNullOrEmpty(mcVersion) && _isEditingInstance && !string.IsNullOrEmpty(_editingVersion))
                    mcVersion = _editingVersion;
                if (loaderType == "vanilla")
                {
                    ProfileModLoaderVersionComboBox.ItemsSource = null;
                    ProfileModLoaderVersionComboBox.Text = "";
                    ProfileModLoaderVersionComboBox.IsEnabled = false;
                    return;
                }
                if (string.IsNullOrEmpty(mcVersion))
                {
                    ProfileModLoaderVersionComboBox.IsEnabled = true;
                    ProfileModLoaderVersionComboBox.ItemsSource = (string[])["Latest"];
                    ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                    return;
                }
                ProfileModLoaderVersionComboBox.IsEnabled = true;
                ProfileModLoaderVersionComboBox.ItemsSource = (string[])["Loading..."];
                ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                if (loaderType == "optifine")
                {
                    try
                    {
                        var builds = await OptiFineClient.GetVersionsForAsync(mcVersion);
                        ProfileModLoaderVersionComboBox.ItemsSource = builds.Count > 0
                            ? builds.Select(b => b.Edition).ToList()
                            : (System.Collections.IEnumerable)(string[])["Not available for this MC version"];
                    }
                    catch { ProfileModLoaderVersionComboBox.ItemsSource = (string[])["Latest"]; }
                    ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                    return;
                }
                try
                {
                    var versions = await ModLoaderInstaller.GetAvailableLoadersAsync(mcVersion);
                    var filtered = versions.Where(v => v.LoaderType.Equals(loaderType, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (filtered.Count > 0)
                    {
                        var versionStrings = filtered.Select(v => v.LoaderVersion).Where(v => !string.IsNullOrEmpty(v)).ToList();
                        if (versionStrings.Count > 0)
                        {
                            ProfileModLoaderVersionComboBox.ItemsSource = versionStrings;
                            ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                        }
                        else
                        {
                            ProfileModLoaderVersionComboBox.ItemsSource = (string[])["Latest"];
                            ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                        }
                    }
                    else
                    {
                        ProfileModLoaderVersionComboBox.ItemsSource = (string[])["Not available for this MC version"];
                        ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error fetching loader versions: {ex.Message}");
                    ProfileModLoaderVersionComboBox.ItemsSource = (string[])["Latest"];
                    ProfileModLoaderVersionComboBox.SelectedIndex = 0;
                }
            }
        }
        private void GameProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance)
                {
                    InstanceManager.Instance.SetSelectedInstance(instance.InstanceId);
                }
                else if (item.Tag is GameProfile profile)
                {
                    GameProfileManager.Instance.SetSelectedProfile(profile.Id);
                }
            }
            UpdateGameProfileInfo();
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                if (tabItem.Header?.ToString() == LocalizedResourcePacks) { LoadResourcePacks(); LoadShaderPacks(); LoadMods(); }
                else if (tabItem.Header?.ToString() == "Server Manager") LoadServerList();
            }
        }
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && MainTabControl.SelectedItem is TabItem selectedTab)
            {
                string header = "";
                if (selectedTab.Header is string str) header = str;
                else if (selectedTab.Header is StackPanel panel && panel.Children.Count > 1 && panel.Children[1] is TextBlock tb) header = tb.Text;
                if (header == LocalizedResourcePacks) { LoadResourcePacks(); LoadShaderPacks(); LoadMods(); }
                else if (header == "Server Manager") LoadServerList();
                else if (ReferenceEquals(selectedTab, ContentTabItem)) InitContentPage();
            }
        }
        private void ResourcePacksTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && ResourcePacksTabControl.SelectedItem is TabItem selected)
            {
                var header = selected.Header?.ToString();
                if (header == "Resource Packs") LoadResourcePacks();
                else if (header == "Shader Packs") LoadShaderPacks();
                else if (header == "Modding") LoadMods();
            }
        }
        private void GetCurrentInstanceVersionInfo(out string version, out string loader)
        {
            version = "";
            loader = "vanilla";
            if (GameProfileComboBox.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is MinecraftInstance instance)
                {
                    version = instance.Version;
                    loader = instance.ModLoader;
                }
                else if (item.Tag is GameProfile profile)
                {
                    version = profile.Version;
                    loader = profile.ModLoader;
                }
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                // default .minecraft profile read what actually installed instead
                var detected = InstalledVersionDetector.Detect(PlatformPaths.GetMinecraftDir());
                version = detected.GameVersion ?? "";
                loader = detected.Loader ?? "vanilla";
            }
            loader = loader.ToLowerInvariant();
        }
        private void UpdateResourcePacksButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out _);
            string path = GetCurrentResourcePacksPath(false);
            new UpdateWindow(path, "resourcepack", version).ShowDialog();
            LoadResourcePacks();
        }
        private void UpdateShaderPacksButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out _);
            string path = GetCurrentResourcePacksPath(true);
            new UpdateWindow(path, "shader", version).ShowDialog();
            LoadShaderPacks();
        }
        private void UpdateModsButton_Click(object sender, RoutedEventArgs e)
        {
            GetCurrentInstanceVersionInfo(out string version, out string loader);
            if (loader == "vanilla" || loader == "none")
            {
                MessageBox.Show("Please select a mod loader (Fabric/Forge) in your profile settings before updating mods.", "No Loader Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string path = GetCurrentModsPath();
            new UpdateWindow(path, "mod", version, loader).ShowDialog();
            LoadMods();
        }
        private void LogMessage(string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (LogTextBox.Text.Length > 400_000)
                    LogTextBox.Text = "[...older log trimmed...]\n" + LogTextBox.Text[^300_000..];
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
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
            UpdateEditViewIcon();
            EditProfileTitle.Text = "Create Profile";
            SaveProfileButton.Content = "Create";
            ShowEditView();
        }
        private void EditInstance_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem item && item.Tag is MinecraftInstance instance)
                {
                    _isEditingInstance = true;
                    _editingInstanceId = instance.InstanceId;
                    _tempIconPath = instance.Icon;
                    _editingVersion = ExtractMcVersion(instance.Version);
                    LoadInstanceIcon(instance);
                    ProfileNameTextBox.Text = instance.Name;
                    if (!string.IsNullOrEmpty(instance.Ram))
                    {
                        string digits = new(instance.Ram.Where(char.IsDigit).ToArray());
                        if (int.TryParse(digits, out int ramValue))
                        {
                            if (ramValue < 64) ramValue *= 1024;
                            ProfileRamSlider.Value = ramValue;
                        }
                        else
                        {
                            ProfileRamSlider.Value = 4096;
                        }
                    }
                    else
                    {
                        ProfileRamSlider.Value = 4096;
                    }
                    string perfTier = instance.PerformanceTier ?? "Auto";
                    foreach (ComboBoxItem cbItem in ProfilePerformanceTierComboBox.Items)
                    {
                        if (cbItem.Tag?.ToString() == perfTier)
                        {
                            ProfilePerformanceTierComboBox.SelectedItem = cbItem;
                            break;
                        }
                    }
                    string effectiveTier = perfTier;
                    if (effectiveTier == "Auto")
                    {
                        effectiveTier = SystemPerformanceHelper.DetectPerformanceTier();
                    }
                    int ramMB = 4096;
                    if (!string.IsNullOrEmpty(instance.Ram))
                    {
                        string digits = new(instance.Ram.Where(char.IsDigit).ToArray());
                        if (int.TryParse(digits, out int val))
                        {
                            ramMB = val < 64 ? val * 1024 : val;
                        }
                    }
                    var perfArgStrings = SystemPerformanceHelper.GetPerformanceArgumentStrings(perfTier, ramMB);
                    string perfArgsStr = string.Join(" ", perfArgStrings);
                    string customArgs = instance.JavaArgs ?? "";
                    string fullArgs = string.IsNullOrWhiteSpace(customArgs)
                        ? perfArgsStr
                        : $"{customArgs} {perfArgStrings}";
                    ProfileJavaArgsTextBox.Text = fullArgs;
                    EditProfileTitle.Text = "Edit Profile";
                    SaveProfileButton.Content = "Save";
                    ShowEditView();
                    if (!string.IsNullOrEmpty(instance.JavaPath) && ProfileJavaComboBox.ItemsSource is List<ComboBoxItem> javaItems)
                    {
                        for (int i = 0; i < javaItems.Count; i++)
                        {
                            if (javaItems[i].Tag as string == instance.JavaPath)
                            {
                                ProfileJavaComboBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    foreach (ComboBoxItem cbItem in ProfileModLoaderComboBox.Items)
                    {
                        if (cbItem.Content?.ToString() is string s && string.Equals(s, instance.ModLoader, StringComparison.OrdinalIgnoreCase))
                        {
                            ProfileModLoaderComboBox.SelectedItem = cbItem;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening edit view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogMessage($"Error opening edit view: {ex.Message}");
            }
        }
        private static string ExtractMcVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return version;
            if (version.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase))
            {
                var lastDash = version.LastIndexOf('-');
                if (lastDash > 0)
                {
                    var candidate = version[(lastDash + 1)..];
                    if (candidate.Split('.').Length >= 2 && char.IsDigit(candidate[0]))
                        return candidate;
                }
            }
            if (version.StartsWith("quilt-loader-", StringComparison.OrdinalIgnoreCase))
            {
                var lastDash = version.LastIndexOf('-');
                if (lastDash > 0)
                {
                    var candidate = version[(lastDash + 1)..];
                    if (candidate.Split('.').Length >= 2 && char.IsDigit(candidate[0]))
                        return candidate;
                }
            }
            if (version.Contains("forge", StringComparison.OrdinalIgnoreCase))
            {
                var dashIdx = version.IndexOf("-forge", StringComparison.OrdinalIgnoreCase);
                if (dashIdx > 0)
                    return version[..dashIdx];
            }
            if (version.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
            {
                var nfVer = version["neoforge-".Length..];
                var parts = nfVer.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                    return $"1.{major}.{minor}";
            }
            return version;
        }
        private void PlayInstance_Click(object sender, RoutedEventArgs e) {}
        private void DuplicateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditingInstance && _editingInstanceId != null)
            {
                var instance = InstanceManager.Instance.GetInstance(_editingInstanceId);
                if (instance != null)
                {
                    try
                    {
                        string newName = $"{instance.Name} (Copy)";
                        int counter = 1;
                        while (InstanceManager.Instance.GetInstanceByName(newName) != null) newName = $"{instance.Name} (Copy {counter++})";
                        var newInstance = InstanceManager.Instance.CreateInstance(newName, instance.Version, instance.ModLoader, instance.Ram);
                        if (newInstance != null)
                        {
                             newInstance.Icon = instance.Icon;
                             InstanceManager.Instance.SaveInstances();
                             LogMessage($"Duplicated to {newName}");
                             HideEditView();
                             LoadGameProfilesList();
                          }
                    }
                    catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
                }
            }
        }
        private async void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = ProfileNameTextBox.Text.Trim();
                string version = ProfileVersionComboBox.Text;
                string modLoader = (ProfileModLoaderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()?.ToLower() ?? "vanilla";
                string loaderVersion = ProfileModLoaderVersionComboBox.Text;
                string ram = ((int)ProfileRamSlider.Value).ToString();
                string javaArgs = ProfileJavaArgsTextBox.Text;
                string performanceTier = (ProfilePerformanceTierComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";
                string? javaPath = (ProfileJavaComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
                if (string.IsNullOrEmpty(name)) { MessageBox.Show("Name empty"); return; }
                string installedVersionName = null;
                if (modLoader != "vanilla" && !string.IsNullOrEmpty(modLoader))
                {
                    try
                    {
                        SaveProfileButton.IsEnabled = false;
                        StatusText = $"Installing {modLoader}...";
                        JavaManager.OnProgressChanged += msg => Dispatcher.Invoke(() => StatusText = msg);
                        JavaManager.OnProgressPercentChanged += pct => Dispatcher.Invoke(() => { });
                        var mcPath = PlatformPaths.GetMinecraftDir();
                        var loaderVer = loaderVersion?.Contains("Not available") == true || loaderVersion == "Latest" ? null : loaderVersion;
                        installedVersionName = await ModLoaderInstaller.InstallLoaderAsync(mcPath, version, modLoader, loaderVer);
                        LogMessage($"Installed {modLoader}: {installedVersionName}");
                    }
                    catch (Exception loaderEx)
                    {
                        MessageBox.Show($"Failed to install {modLoader}: {loaderEx.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        SaveProfileButton.IsEnabled = true;
                        return;
                    }
                    finally
                    {
                        SaveProfileButton.IsEnabled = true;
                        StatusText = "Ready";
                    }
                }
                else
                {
                    installedVersionName = null;
                }
                if (!_isEditingInstance)
                {
                    var newInstance = InstanceManager.Instance.CreateInstance(name, version, modLoader, ram);
                    if (newInstance != null)
                    {
                         newInstance.JavaArgs = javaArgs;
                         newInstance.JavaPath = javaPath;
                         newInstance.Icon = _tempIconPath;
                         newInstance.PerformanceTier = performanceTier;
                         if (!string.IsNullOrEmpty(installedVersionName)) newInstance.InstalledVersionName = installedVersionName;
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
                        instance.Version = version;
                        if (!string.IsNullOrEmpty(installedVersionName)) instance.InstalledVersionName = installedVersionName;
                        instance.ModLoader = modLoader;
                        instance.Ram = ram;
                        instance.JavaArgs = javaArgs;
                        instance.JavaPath = javaPath;
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
            catch (Exception ex) { MessageBox.Show($"Error saving: {ex.Message}"); }
        }
        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
             if (_isEditingInstance && _editingInstanceId != null)
            {
                if (sender is Button) if (MessageBox.Show("Delete profile?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                if (InstanceManager.Instance.RemoveInstance(_editingInstanceId))
                {
                    LogMessage("Profile deleted.");
                    HideEditView();
                    LoadGameProfilesList();
                }
            }
        }
        private async void ImportModpackButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog { Title = "Select Modpack", Filter = "Modpack (*.mrpack)|*.mrpack" };
            if (openFileDialog.ShowDialog() == true)
                await ImportMrpackFileAsync(openFileDialog.FileName);
        }
        private async Task ImportMrpackFileAsync(string path)
        {
            try
            {
                var importer = new ModpackImporter();
                importer.ProgressChanged += (msg, pct) => Dispatcher.Invoke(() => StatusText = $"{msg} ({pct:F0}%)");
                var (success, message, instanceId) = await importer.ImportMrPackAsync(path);
                StatusText = message;
                if (success) { MessageBox.Show(message); LoadGameProfilesList(); if (instanceId != null) { InstanceManager.Instance.SetSelectedInstance(instanceId); LoadGameProfiles(); } }
                else MessageBox.Show(message);
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }
        private void CancelProfileButton_Click(object sender, RoutedEventArgs e) { HideEditView(); }
        private void ShowEditView() { GameProfilesListView.Visibility = Visibility.Collapsed; GameProfilesEditView.Visibility = Visibility.Visible; ConfigureRamSlider(); RefreshJavaComboBoxInProfile(); LoadVersionsAsync(); }
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
        private void HideEditView() { GameProfilesEditView.Visibility = Visibility.Collapsed; GameProfilesListView.Visibility = Visibility.Visible; _isEditingInstance = false; _editingVersion = null; }
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
                // details resolved by LaunchGame, not selectable base versions.
                var versions = await GameProfileManager.Instance.GetVersions();
                var versionIds = versions.Select(v => v.Id).Distinct().ToList();
                ProfileVersionComboBox.ItemsSource = versionIds;
                if (_isEditingInstance && !string.IsNullOrEmpty(_editingVersion))
                {
                    var idx = versionIds.IndexOf(_editingVersion);
                    if (idx >= 0)
                        ProfileVersionComboBox.SelectedIndex = idx;
                    else if (versionIds.Count > 0)
                        ProfileVersionComboBox.SelectedIndex = 0;
                }
                else if (versionIds.Count > 0 && !_isEditingInstance)
                {
                    ProfileVersionComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception) 
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
                foreach (var langCode in LocalizationManager.GetAvailableLanguages()) LanguageComboBox.Items.Add(new ComboBoxItem { Content = LocalizationManager.GetLanguageDisplayName(langCode), Tag = langCode });
                NewsBrowserComboBox.SelectedIndex = 0;
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
                UiStyleComboBox.SelectedIndex = 0; // this UI is "wpf"; handler ignores until user changes it
                _uiStyleReady = true;
                AddMicrosoftAccountButton.Click += AddMicrosoftAccountButton_Click;
                AddOfflineAccountButton.Click += AddOfflineAccountButton_Click;
                DiscordRpcCheckBox.Checked += (s, e) => { SaveSettings(); InitializeDiscordRpc(); };
                DiscordRpcCheckBox.Unchecked += (s, e) => { SaveSettings(); InitializeDiscordRpc(); };
                DeleteTelemetryCheckBox.Checked += (s, e) => SaveSettings();
                DeleteTelemetryCheckBox.Unchecked += (s, e) => SaveSettings();
                DebugModeCheckBox.Checked += DebugModeCheckBox_Checked;
                DebugModeCheckBox.Unchecked += DebugModeCheckBox_Unchecked;
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
                AccountsListBox.SelectionChanged += AccountsListBox_SelectionChanged;
                ShowSettingsPanel("General");
                InitializeDiscordRpc();
            }
            catch (Exception ex) { LogMessage($"Error initializing settings: {ex.Message}"); }
        }
        private async void AccountsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadCurrentAccountSkinAsync();
        }
        private void RefreshJavaVersionsList()
        {
            var installText = LocalizationManager.GetString("SETTINGS_JAVA_INSTALL", "Install");
            var uninstallText = LocalizationManager.GetString("SETTINGS_JAVA_UNINSTALL", "Uninstall");
            var installedText = LocalizationManager.GetString("SETTINGS_JAVA_INSTALLED", "Installed");
            var notInstalledText = LocalizationManager.GetString("SETTINGS_JAVA_NOT_INSTALLED", "Not installed");
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
                        ? $"Java {ver} (Adoptium, {archLabel} - no 32-bit build available)"
                        : $"Java {ver} (Adoptium, {archLabel})",
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
            if (sender is Button btn && btn.Tag is int version)
            {
                try
                {
                    btn.IsEnabled = false;
                    StatusText = $"Installing Java {version}...";
                    JavaManager.OnProgressChanged += msg => Dispatcher.Invoke(() => StatusText = msg);
                    JavaManager.OnProgressPercentChanged += pct => Dispatcher.Invoke(() => { });
                    await JavaManager.InstallJavaAsync(version);
                    LogMessage($"Java {version} installed successfully");
                    RefreshJavaVersionsList();
                    StatusText = $"Java {version} installed!";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to install Java {version}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    LogMessage($"Java {version} install failed: {ex.Message}");
                    StatusText = "Ready";
                }
                finally
                {
                    btn.IsEnabled = true;
                }
            }
        }
        private void UninstallJavaButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int version)
            {
                try
                {
                    JavaManager.UninstallJava(version);
                    LogMessage($"Java {version} uninstalled");
                    RefreshJavaVersionsList();
                    StatusText = $"Java {version} uninstalled";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to uninstall Java {version}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void RefreshJavaComboBoxInProfile()
        {
            var autoText = LocalizationManager.GetString("SETTINGS_JAVA_AUTO", "Automatic (Detect)");
            var items = new List<ComboBoxItem>
            {
                new() { Content = autoText, Tag = (string?)null }
            };
            foreach (var java in JavaManager.GetAllJavaInstallations())
            {
                items.Add(new ComboBoxItem { Content = java.DisplayName, Tag = java.Path });
            }
            ProfileJavaComboBox.ItemsSource = items;
            ProfileJavaComboBox.SelectedIndex = 0;
        }
        private void ShowSettingsPanel(string panelName)
        {
            GeneralSettingsPanel.Visibility = Visibility.Collapsed;
            AccountsSettingsPanel.Visibility = Visibility.Collapsed;
            AdvancedSettingsPanel.Visibility = Visibility.Collapsed;
            AboutSettingsPanel.Visibility = Visibility.Collapsed;
            GeneralSettingsButton.ClearValue(Control.BackgroundProperty);
            GeneralSettingsButton.ClearValue(Control.ForegroundProperty);
            AccountsSettingsButton.ClearValue(Control.BackgroundProperty);
            AccountsSettingsButton.ClearValue(Control.ForegroundProperty);
            AdvancedSettingsButton.ClearValue(Control.BackgroundProperty);
            AdvancedSettingsButton.ClearValue(Control.ForegroundProperty);
            AboutSettingsButton.ClearValue(Control.BackgroundProperty);
            AboutSettingsButton.ClearValue(Control.ForegroundProperty);
            switch (panelName)
            {
                case "General": GeneralSettingsPanel.Visibility = Visibility.Visible; GeneralSettingsButton.SetResourceReference(Control.BackgroundProperty, "AccentPrimaryBrush"); GeneralSettingsButton.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush"); break;
                case "Accounts": 
                    AccountsSettingsPanel.Visibility = Visibility.Visible; 
                    AccountsSettingsButton.SetResourceReference(Control.BackgroundProperty, "AccentPrimaryBrush"); 
                    AccountsSettingsButton.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush"); 
                    _ = LoadCurrentAccountSkinAsync();
                    break;
                case "Advanced": AdvancedSettingsPanel.Visibility = Visibility.Visible; AdvancedSettingsButton.SetResourceReference(Control.BackgroundProperty, "AccentPrimaryBrush"); AdvancedSettingsButton.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush"); RefreshJavaVersionsList(); break;
                case "About": AboutSettingsPanel.Visibility = Visibility.Visible; AboutSettingsButton.SetResourceReference(Control.BackgroundProperty, "AccentPrimaryBrush"); AboutSettingsButton.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush"); break;
            }
        }
        private void LoadSettingsData()
        {
            try
            {
                var configPath = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    if (config != null)
                    {
                        if (config.TryGetValue("language", out var language)) 
                        { 
                            var langCode = language.GetString() ?? "en-US"; 
                            foreach (ComboBoxItem item in LanguageComboBox.Items) 
                            { 
                                if (item.Tag?.ToString() == langCode) 
                                { 
                                    LanguageComboBox.SelectedItem = item; 
                                    break; 
                                } 
                            } 
                            LocalizationManager.LoadLanguage(langCode); 
                            RefreshLocalizedStrings();
                        }
                        if (config.TryGetValue("theme", out var theme))
                        {
                            var themeName = theme.GetString() ?? "arc";
                            themeName = themeName switch
                            {
                                "arc" or "dark_prism" or "light" => themeName,
                                "dark" => "dark_prism",
                                _ => "arc"
                            };
                            bool matched = false;
                            foreach (ComboBoxItem item in ThemeComboBox.Items)
                            {
                                if (item.Tag?.ToString() == themeName)
                                {
                                    ThemeComboBox.SelectedItem = item;
                                    matched = true;
                                    break;
                                }
                            }
                            if (!matched) ThemeComboBox.SelectedIndex = 0;
                        }
                        else
                        {
                            ThemeComboBox.SelectedIndex = 0;
                        }
                        if (config.TryGetValue("discordRpcEnabled", out var discordRpc)) 
                        {
                            try { DiscordRpcCheckBox.IsChecked = discordRpc.GetBoolean(); } 
                            catch { DiscordRpcCheckBox.IsChecked = true; }
                        }
                        if (config.TryGetValue("deleteTelemetryOnStartup", out var telemetry)) 
                        {
                            try { DeleteTelemetryCheckBox.IsChecked = telemetry.GetBoolean(); }
                            catch { DeleteTelemetryCheckBox.IsChecked = false; }
                        }
                        if (config.TryGetValue("debugModeEnabled", out var debug)) 
                        { 
                            try 
                            { 
                                var debugVal = debug.GetBoolean();
                                DebugModeCheckBox.IsChecked = debugVal; 
                                DebugInfoTextBox.Visibility = debugVal ? Visibility.Visible : Visibility.Collapsed; 
                            }
                            catch { DebugModeCheckBox.IsChecked = false; }
                        }
                        if (config.TryGetValue("newsBrowser", out var newsBrowser)) 
                        { 
                            var browser = newsBrowser.GetString() ?? "webview2"; 
                            foreach (ComboBoxItem item in NewsBrowserComboBox.Items) 
                            { 
                                if (item.Tag?.ToString() == browser) 
                                { 
                                    NewsBrowserComboBox.SelectedItem = item; 
                                    break; 
                                } 
                            } 
                        }
                        if (config.TryGetValue("useDiscreteGpu", out var discreteGpu))
                        {
                            try { UseDiscreteGpuCheckBox.IsChecked = discreteGpu.GetBoolean(); }
                            catch { UseDiscreteGpuCheckBox.IsChecked = false; }
                        }
                    }
                }
                else 
                { 
                    LanguageComboBox.SelectedIndex = 0; 
                    ThemeComboBox.SelectedIndex = 0; 
                    DiscordRpcCheckBox.IsChecked = true; 
                    DeleteTelemetryCheckBox.IsChecked = false;
                    UseDiscreteGpuCheckBox.IsChecked = false;
                }
            }
            catch { }
        }
        private void ApplyInitialTheme()
        {
            try { if (ThemeComboBox.SelectedItem is ComboBoxItem selectedThemeItem && selectedThemeItem.Tag is string themeKey) ThemeManager.ApplyTheme(themeKey); else ThemeManager.ApplyTheme("arc"); }
            catch { try { ThemeManager.ApplyTheme("arc"); } catch {} }
        }
        private void LoadAccountsData()
        {
            try
            {
                _userProfiles.Clear();
                var profiles = ProfileManager.Instance.GetProfiles();
                foreach (var profile in profiles) _userProfiles.Add(profile);
                AccountsListBox.ItemsSource = _userProfiles;
            }
            catch {}
        }
        private void LoadPluginsData() { try { _plugins.Clear(); var pluginsPath = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_plugins.json"); if (File.Exists(pluginsPath)) { var json = File.ReadAllText(pluginsPath); var plugins = JsonSerializer.Deserialize<List<PluginInfo>>(json); if (plugins != null) foreach (var p in plugins) _plugins.Add(p); } PluginsListBox.ItemsSource = _plugins; } catch {} }
        private void SaveSettings()
        {
            try
            {
                var configPath = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(configPath)!);
                var config = new Dictionary<string, object>();
                try
                {
                    if (File.Exists(configPath))
                    {
                        var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(configPath));
                        if (existing != null)
                            foreach (var kv in existing) config[kv.Key] = kv.Value;
                    }
                }
                catch { }
                config["language"] = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en-US";
                config["theme"] = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "arc";
                config["discordRpcEnabled"] = DiscordRpcCheckBox.IsChecked ?? true;
                config["deleteTelemetryOnStartup"] = DeleteTelemetryCheckBox.IsChecked ?? false;
                config["debugModeEnabled"] = DebugModeCheckBox.IsChecked ?? false;
                config["newsBrowser"] = (NewsBrowserComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "webview2";
                config["useDiscreteGpu"] = UseDiscreteGpuCheckBox.IsChecked ?? false;
                File.WriteAllText(configPath, JsonSerializer.Serialize(config, _jsonSerializerOptions));
            }
            catch {}
        }
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string languageCode)
            {
                SaveSettings();
                LocalizationManager.LoadLanguage(languageCode);
                RefreshLocalizedStrings(); 
                if (MessageBox.Show(LocalizationManager.GetString("LANGUAGE_CHANGED_MSG"), LocalizationManager.GetString("LANGUAGE_CHANGED_TITLE"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) RestartApplication();
            }
        }
        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string themeKey)
            {
                ThemeManager.ApplyTheme(themeKey);
                SaveSettings();
            }
        }
        private bool _uiStyleReady;
        private void UiStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiStyleReady) return;
            if (UiStyleComboBox.SelectedItem is ComboBoxItem item && item.Tag is string ui && ui != UiModeManager.Wpf)
            {
                UiModeManager.SwitchTo(ui); // saves choice and restarts the launcher
            }
        }
        private async void AddMicrosoftAccountButton_Click(object sender, RoutedEventArgs e)
        {
             var loginWindow = new MicrosoftLoginDialog { Owner = this };
             if (loginWindow.ShowDialog() == true && loginWindow.ResultProfile != null)
             {
                 var profile = loginWindow.ResultProfile;
                 ProfileManager.Instance.AddOrUpdateProfile(profile);
                 LoadAccountsData();
                 LoadProfiles();
                 MessageBox.Show($"Microsoft account {profile.Username} added successfully!", "Success");
             }
             await Task.CompletedTask;
        }
        private void AddOfflineAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var username = string.Empty;
            var dlg = new AddTextDialog("Enter offline username:", "Add Offline Account", "") { Owner = this };
            if (dlg.ShowDialog() == true) username = dlg.ResultText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(username))
            {
                ProfileManager.Instance.AddProfile(username.Trim(), "Offline");
                LoadAccountsData();
                LoadProfiles(); 
            }
        }
        private void DebugModeCheckBox_Checked(object sender, RoutedEventArgs e) { DebugInfoTextBox.Visibility = Visibility.Visible; SaveSettings(); UpdateDebugInfo(); }
        private void DebugModeCheckBox_Unchecked(object sender, RoutedEventArgs e) { DebugInfoTextBox.Visibility = Visibility.Collapsed; SaveSettings(); }
        private void AddPluginButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Plugin DLL files (*.dll)|*.dll",
                Title = LocalizationManager.GetString("PLUGIN_SELECT_TITLE", "Select Plugin File")
            };
            if (dlg.ShowDialog() == true)
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
                StatusText = "Checking for updates...";
                var updateInfo = await UpdateManager.CheckForUpdatesAsync();
                string releaseLabel = string.IsNullOrWhiteSpace(updateInfo.ReleaseName)
                    ? updateInfo.NewVersion
                    : $"{updateInfo.ReleaseName} ({updateInfo.NewVersion})";
                if (updateInfo.UpdateAvailable)
                {
                    MessageBox.Show(
                        $"A new version is available.\n\n" +
                        $"Current version: {updateInfo.CurrentVersion}\n" +
                        $"Latest release: {releaseLabel}\n\n" +
                        $"Visit the GitHub releases page to download manually.",
                        "Update Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    UpdateManager.OpenReleasesPage();
                }
                else
                {
                    MessageBox.Show(
                        $"You are running the latest version.\n\nCurrent: {updateInfo.CurrentVersion}\nLatest release: {releaseLabel}",
                        "No Updates",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                StatusText = "Ready";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to check for updates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "Ready";
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
            }
        }
        private void NewsBrowserComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) 
        { 
            if (NewsBrowserComboBox.SelectedItem is ComboBoxItem item && item.Tag is string browser) 
            { 
                SaveSettings(); 
                var previousBrowser = "webview2";
                try 
                {
                    var path = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json");
                    if (File.Exists(path))
                    {
                        var json = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
                        if (json != null && json.TryGetValue("newsBrowser", out var b))
                        {
                            previousBrowser = b?.ToString() ?? "webview2";
                        }
                    }
                }
                catch { }
                bool switchingToIE = (browser == "ie" && previousBrowser != "ie");
                bool switchingFromIE = (browser != "ie" && previousBrowser == "ie");
                if (switchingToIE || switchingFromIE)
                {
                    MessageBox.Show("The launcher will now restart to apply browser changes.", "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    RestartApplication();
                }
                else
                {
                    ApplyNewsBrowser(browser);
                }
            } 
        }
        private void OpenGitHubButton_Click(object sender, RoutedEventArgs e) { UpdateManager.OpenGitHubPage(); }
        private void SavePlugins() { try { var path = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_plugins.json"); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(_plugins.ToList(), _jsonSerializerOptions)); } catch {} }
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
        private void UpdateDebugInfo() { if (DebugInfoTextBox.Visibility == Visibility.Visible) DebugInfoTextBox.Text = $"OS: {Environment.OSVersion}\n.NET: {Environment.Version}\nData: {PlatformPaths.GetDataDir()}"; }
        private void RestartApplication() { var path = Environment.ProcessPath; if (path != null) { Process.Start(path); Application.Current.Shutdown(); } }
        private async Task InitializeNewsBrowser()
        {
            try
            {
                var path = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json");
                string browser = "webview2";
                if (File.Exists(path))
                {
                    var json = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
                    if (json != null && json.TryGetValue("newsBrowser", out var b))
                    {
                        browser = b?.ToString() ?? "webview2";
                    }
                }
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
                else
                {
                    LogMessage($"Skipping WebView2 initialization ({browser} selected)");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error during browser initialization: {ex.Message}");
            }
        }
        private void ApplyNewsBrowserFromSettings() { try { var path = System.IO.Path.Combine(PlatformPaths.GetDataDir(), "launcher_config.json"); if (File.Exists(path)) { var json = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path)); if (json != null && json.TryGetValue("newsBrowser", out var b)) ApplyNewsBrowser(b?.ToString() ?? "webview2"); else ApplyNewsBrowser("webview2"); } else ApplyNewsBrowser("webview2"); } catch { ApplyNewsBrowser("webview2"); } }
        private void ApplyNewsBrowser(string browser)
        {
            try
            {
                string url = "https://oranges.lt/launcher.html";
                if (browser == "native")
                {
                    try
                    {
                        NewsWebBrowser.Navigate("about:blank");
                    }
                    catch { }
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
                    try
                    {
                        NewsWebBrowser.Navigate("about:blank");
                    }
                    catch { }
                    NewsWebBrowser.Visibility = Visibility.Collapsed;
                    NewsWebView.Visibility = Visibility.Visible;
                    if (NewsWebView.CoreWebView2 != null) NewsWebView.CoreWebView2.Navigate(url);
                    else NewsWebView.Source = new Uri(url);
                }
            }
            catch { }
        }
        private void NewsNativeBrowserLink_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo { FileName = "https://oranges.lt/launcher.html", UseShellExecute = true }); }
            catch { }
        }
        private static readonly HttpClient _newsHttp = new() { Timeout = TimeSpan.FromSeconds(20) };
        private async Task LoadNativeNewsAsync(string url)
        {
            NewsNativeTitle.Text = LocalizationManager.GetString("UPDATE_NOTES", "Update Notes");
            NewsNativeBrowserText.Text = LocalizationManager.GetString("NEWS_OPEN_BROWSER", "Open news in browser");
            NewsNativeStack.Children.Clear();
            try
            {
                NewsNativeStack.Children.Add(NewsParagraph(LocalizationManager.GetString("NEWS_LOADING", "Loading news...")));
                var html = await _newsHttp.GetStringAsync(url);
                NewsNativeStack.Children.Clear();
                foreach (var el in BuildNewsBlocks(html)) NewsNativeStack.Children.Add(el);
                if (NewsNativeStack.Children.Count == 0)
                    NewsNativeStack.Children.Add(NewsParagraph(LocalizationManager.GetString("CONTENT_NO_RESULTS", "No results.")));
            }
            catch (Exception ex)
            {
                NewsNativeStack.Children.Clear();
                NewsNativeStack.Children.Add(NewsParagraph(
                    $"{LocalizationManager.GetString("NEWS_LOAD_FAILED", "Could not load news:")} {ex.Message}"));
            }
        }
        private TextBlock NewsParagraph(string text) => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Opacity = 0.9,
            Margin = new Thickness(0, 2, 0, 2),
            Foreground = (System.Windows.Media.Brush)FindResource("FgPrimaryBrush")
        };
        // turns the news HTML into styled blocks
        private IEnumerable<UIElement> BuildNewsBlocks(string html)
        {
            var blocks = new List<UIElement>();
            html = Regex.Replace(html, "<(script|style|head)[^>]*>.*?</\\1>", "",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var matches = Regex.Matches(html, "<(?<tag>h[1-6]|p|li)[^>]*>(?<body>.*?)</\\k<tag>>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
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
                            blocks.Add(new Border
                            {
                                Height = 1,
                                Margin = new Thickness(0, 14, 0, 10),
                                Background = (System.Windows.Media.Brush)FindResource("BorderBrush")
                            });
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
        private void SuppressScriptErrors(WebBrowser webBrowser, bool hide)
        {
            try
            {
                var fiComWebBrowser = typeof(WebBrowser).GetField("_axIWebBrowser2", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (fiComWebBrowser == null) return;
                object? objComWebBrowser = fiComWebBrowser.GetValue(webBrowser);
                if (objComWebBrowser == null)
                {
                    webBrowser.Navigated += (s, e) => 
                    {
                        try
                        {
                            var field = typeof(WebBrowser).GetField("_axIWebBrowser2", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (field != null)
                            {
                                var obj = field.GetValue(webBrowser);
                                if (obj != null)
                                {
                                    obj.GetType().InvokeMember("Silent", System.Reflection.BindingFlags.SetProperty, null, obj, [hide]);
                                }
                            }
                        }
                        catch { }
                    };
                    return;
                }
                objComWebBrowser.GetType().InvokeMember("Silent", System.Reflection.BindingFlags.SetProperty, null, objComWebBrowser, [hide]);
            }
            catch { }
        }
        private void InjectScriptErrorHandler(WebBrowser webBrowser)
        {
            try
            {
                dynamic? doc = webBrowser.Document;
                if (doc != null)
                {
                    webBrowser.InvokeScript("eval", ["window.onerror = function(msg, url, line) { return true; };"]);
                }
            }
            catch { }
        }
        private async Task LoadCurrentAccountSkinAsync()
        {
            UserProfile? currentProfile = null;
            if (AccountsListBox.SelectedItem is UserProfile profile)
            {
                currentProfile = profile;
            }
            else if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile comboProfile)
            {
                currentProfile = comboProfile;
            }
            if (currentProfile == null || string.IsNullOrEmpty(currentProfile.Username))
            {
                SkinInfoText.Text = "Select an account to view skin.";
                SkinBodyImage.Source = null;
                SkinBodyViewer.Clear();
                return;
            }
            try
            {
                SkinInfoText.Text = "Loading skin...";
                SkinBodyImage.Source = null;
                PlayerSkinInfo? skinInfo = null;
                if (currentProfile.Type == "Microsoft")
                {
                    var token = currentProfile.MinecraftToken ?? currentProfile.AccessToken;
                    if (!string.IsNullOrEmpty(token))
                    {
                        skinInfo = await SkinManager.GetPlayerSkinWithTokenAsync(token);
                    }
                }
                if (skinInfo == null)
                {
                    skinInfo = await SkinManager.GetPlayerSkinAsync(currentProfile.Username);
                }
                if (skinInfo != null)
                {
                    // native 3D preview from the raw skin.
                    byte[]? skinBytes = string.IsNullOrEmpty(skinInfo.SkinUrl) ? null : await SkinManager.GetSkinBytesAsync(skinInfo.SkinUrl);
                    byte[]? capeBytes = string.IsNullOrEmpty(skinInfo.CapeUrl) ? null : await SkinManager.GetSkinBytesAsync(skinInfo.CapeUrl);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        bool rendered3d = skinBytes != null && SkinBodyViewer.LoadSkin(skinBytes, skinInfo.IsSlim);
                        if (rendered3d) SkinBodyViewer.LoadCape(capeBytes);
                        SkinBodyViewer.Visibility = rendered3d ? Visibility.Visible : Visibility.Collapsed;
                        SkinBodyImage.Visibility = rendered3d ? Visibility.Collapsed : Visibility.Visible;
                        if (!rendered3d) SkinBodyImage.Source = skinInfo.SkinImage;
                        if (skinInfo.IsSlim)
                        {
                            SkinModelAlex.IsChecked = true;
                        }
                        else
                        {
                            SkinModelSteve.IsChecked = true;
                        }
                        SkinInfoText.Text = $"Username: {skinInfo.Username}\n" +
                                           $"UUID: {skinInfo.Uuid}\n" +
                                           $"Model: {(skinInfo.IsSlim ? "Alex (slim)" : "Steve (classic)")}\n" +
                                           $"Has Cape: {(!string.IsNullOrEmpty(skinInfo.CapeUrl) ? "Yes" : "No")}";
                    });
                }
                else
                {
                    SkinInfoText.Text = "Could not find player or skin data.";
                    SkinBodyImage.Source = null;
                    SkinBodyViewer.Clear();
                }
            }
            catch (Exception ex)
            {
                SkinInfoText.Text = $"Error: {ex.Message}";
            }
        }
        private async void ResetSkinButton_Click(object sender, RoutedEventArgs e)
        {
            UserProfile? msAccount = null;
            if (AccountsListBox.SelectedItem is UserProfile profile && profile.Type == "Microsoft")
            {
                msAccount = profile;
            }
            else if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile comboProfile && comboProfile.Type == "Microsoft")
            {
                msAccount = comboProfile;
            }
            if (msAccount == null || string.IsNullOrEmpty(msAccount.AccessToken))
            {
                MessageBox.Show("You need to log in with a Microsoft account to reset your skin.", "Login Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var result = MessageBox.Show("Are you sure you want to reset your skin to the default?", "Reset Skin", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
            try
            {
                ResetSkinButton.IsEnabled = false;
                SkinInfoText.Text = "Resetting skin...";
                var success = await SkinManager.ResetSkinAsync(msAccount.AccessToken);
                if (success)
                {
                    SkinInfoText.Text = "Skin reset successfully! Refreshing preview...";
                    await Task.Delay(2000);
                    await LoadCurrentAccountSkinAsync();
                }
                else
                {
                    SkinInfoText.Text = "Failed to reset skin.";
                }
            }
            catch (Exception ex)
            {
                SkinInfoText.Text = $"Error resetting skin: {ex.Message}";
            }
            finally
            {
                ResetSkinButton.IsEnabled = true;
            }
        }
        private async void ChangeSkinButton_Click(object sender, RoutedEventArgs e)
        {
            UserProfile? msAccount = null;
            if (AccountsListBox.SelectedItem is UserProfile profile && profile.Type == "Microsoft")
            {
                msAccount = profile;
            }
            else if (ProfileComboBox.SelectedItem is ComboBoxItem item && item.Tag is UserProfile comboProfile && comboProfile.Type == "Microsoft")
            {
                msAccount = comboProfile;
            }
            else
            {
                var profiles = ProfileManager.Instance.GetProfiles();
                msAccount = profiles.FirstOrDefault(p => p.Type == "Microsoft");
            }
            if (msAccount == null || string.IsNullOrEmpty(msAccount.AccessToken))
            {
                MessageBox.Show("You need to log in with a Microsoft account to change your skin.", "Login Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Select Minecraft Skin (64x64 or 64x32 PNG)"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    ChangeSkinButton.IsEnabled = false;
                    SkinInfoText.Text = "Uploading skin...";
                    var success = await SkinManager.UploadSkinAsync(msAccount.AccessToken, dlg.FileName, true);
                    if (success)
                    {
                        SkinInfoText.Text = "Skin uploaded successfully! Refreshing preview...";
                        await Task.Delay(2000);
                        await LoadCurrentAccountSkinAsync();
                    }
                    else
                    {
                        SkinInfoText.Text = "Failed to upload skin. Check if your account has Minecraft ownership.";
                    }
                }
                catch (Exception ex)
                {
                    SkinInfoText.Text = $"Error uploading skin: {ex.Message}";
                }
                finally
                {
                    ChangeSkinButton.IsEnabled = true;
                }
            }
        }
    }
}