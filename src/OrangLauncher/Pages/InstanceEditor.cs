using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrangLauncher.Managers;
using OrangLauncher.Models;
using static OrangLauncher.UiKit;
namespace OrangLauncher.Pages
{
    public class InstanceEditor : DockPanel
    {
        private static readonly (string Key, string TKey, string Default)[] Tabs =
        [
            ("custom", "QT_TAB_CUSTOMIZATION", "Customization"),
            ("memvideo", "QT_TAB_MEM_VIDEO", "Mem and Video"),
            ("modpack", "QT_TAB_MODPACK", "Modpack Management"),
            ("mods", "QT_TAB_MODS", "Mods"),
            ("servers", "QT_TAB_SERVERS_WORLDS", "Servers and Worlds"),
            ("packs", "QT_TAB_PACKS", "Resource packs and Shader packs"),
            ("sharing", "QT_TAB_SHARING", "Sharing"),
            ("screenshots", "QT_TAB_SCREENSHOTS", "Screenshots"),
            ("other", "QT_TAB_OTHER", "Other settings"),
            ("versions", "QT_TAB_VERSIONS", "Versions")
        ];
        private readonly MainWindow _launcher;
        private readonly Action _back;
        private readonly TabControl _tabs = new();
        private readonly HashSet<string> _loadedTabs = new();
        private readonly Image _headerIcon = new() { Width = 56, Height = 56 };
        private readonly TextBlock _title = Label("", "h1");
        private readonly TextBlock _subtitle = Label("", "muted");
        public MinecraftInstance? Instance { get; private set; }
        private readonly ModsPanel _mods;
        private readonly ServersWorldsPanel _servers;
        private readonly PacksPanel _packs;
        public InstanceEditor(MainWindow launcher, Action onBack)
        {
            _launcher = launcher;
            _back = onBack;
            UseOriginalStyles(this);
            Margin = new Thickness(18, 12, 18, 10);
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var back = Button(T("GAME_PROFILES_GO_BACK", "← Back"), onBack, "flat");
            back.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(back, Dock.Left);
            head.Children.Add(back);
            _headerIcon.Margin = new Thickness(0, 0, 12, 0);
            RenderOptions.SetBitmapScalingMode(_headerIcon, BitmapScalingMode.HighQuality);
            DockPanel.SetDock(_headerIcon, Dock.Left);
            head.Children.Add(_headerIcon);
            var right = Row(8,
                Button(T("PLAY", "Play"), Play, "accent"),
                Button(T("RES_SH_OPEN_FOLDER", "Open folder"), OpenFolder, "", "folder"));
            right.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(right, Dock.Right);
            head.Children.Add(right);
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _title.TextTrimming = TextTrimming.CharacterEllipsis;
            col.Children.Add(_title);
            col.Children.Add(_subtitle);
            head.Children.Add(col);
            SetDock(head, Dock.Top);
            Children.Add(head);
            _mods = new ModsPanel(launcher);
            _servers = new ServersWorldsPanel(launcher);
            _packs = new PacksPanel(launcher);
            foreach (var (key, tkey, def) in Tabs)
            {
                FrameworkElement content = key switch
                {
                    "custom" => BuildCustomization(),
                    "memvideo" => BuildMemVideo(),
                    "modpack" => BuildModpack(),
                    "mods" => _mods,
                    "servers" => _servers,
                    "packs" => _packs,
                    "sharing" => BuildSharing(),
                    "screenshots" => BuildScreenshots(),
                    "other" => BuildOther(),
                    _ => BuildVersions()
                };
                _tabs.Items.Add(new TabItem { Header = T(tkey, def), Content = content, Tag = key });
            }
            _tabs.SelectionChanged += (s, e) => { if (e.OriginalSource == _tabs) LoadTab(); };
            Children.Add(_tabs);
        }
        public void Open(MinecraftInstance instance, string? tab = null)
        {
            Instance = instance;
            _loadedTabs.Clear();
            RefreshHeader();
            var target = tab == null ? null : _tabs.Items.OfType<TabItem>().FirstOrDefault(t => Equals(t.Tag, tab));
            if (target != null) _tabs.SelectedItem = target;
            else if (_tabs.SelectedIndex < 0) _tabs.SelectedIndex = 0;
            LoadTab();
        }
        public void RefreshHeader()
        {
            var inst = Instance;
            if (inst == null) return;
            _headerIcon.Source = InstanceIcons.For(inst, 56);
            _title.Text = inst.Name;
            var mb = inst.RamMb;
            var ram = mb % 1024 == 0 ? $"{mb / 1024} GB" : $"{mb / 1024.0:0.0} GB";
            var loader = char.ToUpper(inst.LoaderKey[0]) + inst.LoaderKey[1..];
            _subtitle.Text = $"Minecraft {inst.Version}  ·  {loader}" + (string.IsNullOrEmpty(inst.LoaderVersion) ? "" : $" {inst.LoaderVersion}") + $"  ·  {ram} " + T("QT_RAM", "RAM");
        }
        private void LoadTab()
        {
            if (Instance == null || _tabs.SelectedItem is not TabItem tab || tab.Tag is not string key) return;
            if (!_loadedTabs.Add(key)) return;
            try
            {
                switch (key)
                {
                    case "custom": LoadCustomization(); break;
                    case "memvideo": LoadMemVideo(); break;
                    case "modpack": LoadModpack(); break;
                    case "mods": _mods.Load(Instance); break;
                    case "servers": _servers.Load(Instance); break;
                    case "packs": _packs.Load(Instance); break;
                    case "sharing": LoadSharing(); break;
                    case "screenshots": LoadScreenshots(); break;
                    case "other": LoadOther(); break;
                    case "versions": LoadVersions(); break;
                }
            }
            catch (Exception ex)
            {
                _launcher.AppendLog($"[Editor] load {key}: {ex.Message}");
            }
        }
        private void Play()
        {
            if (Instance == null) return;
            InstanceManager.Instance.SetSelectedInstance(Instance.InstanceId);
            _launcher.LaunchGame();
        }
        private void OpenFolder()
        {
            if (Instance == null) return;
            Directory.CreateDirectory(Instance.MinecraftDir);
            OpenPath(Instance.MinecraftDir);
        }
        private bool Save(string? message = null)
        {
            try { InstanceManager.Instance.SaveInstances(); }
            catch (Exception ex)
            {
                Error(T("QT_SAVE", "Save"), ex.Message);
                return false;
            }
            RefreshHeader();
            if (message != null) _launcher.SetStatus(message);
            return true;
        }
        private static FrameworkElement SaveRow(Action save) => Bar(Button(T("QT_SAVE", "Save"), save, "accent"));

        private Image _custIcon = null!;
        private ComboBox _custIconChoice = null!;
        private TextBox _custName = null!;
        private TextBox _custPath = null!;
        private string _custIconPath = "";
        private bool _custFilling;
        private FrameworkElement BuildCustomization()
        {
            var (scroll, body) = Page(new Thickness(18, 16, 18, 16));
            var card = Card(T("QT_LOOK", "Look"), out var cb);
            var row = new DockPanel();
            _custIcon = new Image { Width = 64, Height = 64, Margin = new Thickness(0, 0, 14, 0) };
            RenderOptions.SetBitmapScalingMode(_custIcon, BitmapScalingMode.HighQuality);
            DockPanel.SetDock(_custIcon, Dock.Left);
            row.Children.Add(_custIcon);
            var col = new StackPanel();
            col.Children.Add(Label(T("QT_ICON", "Icon"), "muted"));
            _custIconChoice = Combo(200);
            _custIconChoice.Items.Add(Item(T("QT_ICON_LOADER_DEFAULT", "Loader default"), "__default__"));
            foreach (var l in LoaderVersionService.Loaders) _custIconChoice.Items.Add(Item(l, l));
            _custIconChoice.Items.Add(Item(T("QT_ICON_CUSTOM", "Custom image..."), "__custom__"));
            _custIconChoice.SelectionChanged += (_, _) => OnIconChoice();
            var line = Bar(_custIconChoice,
                Button(T("QT_BROWSE_IMAGE", "Browse image"), BrowseIcon, "", "folder"),
                Button(T("QT_GENERATE_ICON", "Generate"), () => { _custIconPath = InstanceIcons.Generate(); SetIconChoice("__custom__"); PreviewIcon(); }, "", "refresh"));
            line.Margin = new Thickness(0, 4, 0, 0);
            col.Children.Add(line);
            col.Children.Add(Label(T("QT_ICON_HINT", "PNG / JPG, shown on the instance card and in the header."), "hint"));
            row.Children.Add(col);
            cb.Children.Add(row);
            body.Children.Add(card);
            var card2 = Card(T("QT_IDENTITY", "Identity"), out var cb2);
            _custName = TextBox();
            cb2.Children.Add(FormRow(T("QT_INSTANCE_NAME", "Instance name"), _custName));
            _custPath = TextBox();
            _custPath.IsReadOnly = true;
            var pathRow = new DockPanel();
            var pathButtons = Row(8, Button(T("GAME_PROFILES_OPEN_BTN", "Open"), OpenFolder, "", "folder"));
            pathButtons.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(pathButtons, Dock.Right);
            pathRow.Children.Add(pathButtons);
            pathRow.Children.Add(_custPath);
            cb2.Children.Add(FormRow(T("QT_INSTANCE_PATH", "Instance path"), pathRow));
            body.Children.Add(card2);
            body.Children.Add(SaveRow(SaveCustomization));
            return scroll;
        }
        private void SetIconChoice(string tag)
        {
            _custFilling = true;
            SelectTag(_custIconChoice, tag);
            _custFilling = false;
        }
        private void OnIconChoice()
        {
            if (_custFilling || SelectedTag(_custIconChoice) is not string tag) return;
            if (tag == "__custom__") { BrowseIcon(); return; }
            _custIconPath = tag == "__default__" ? "" : InstanceIcons.LoaderIconPath(tag) ?? "";
            PreviewIcon();
        }
        private void BrowseIcon()
        {
            var p = PickFile(T("QT_SELECT_ICON", "Select icon image"), Filter((T("QT_FT_IMAGES", "Images"), "*.png;*.jpg;*.jpeg;*.gif;*.bmp")) + "|" + AllFiles);
            if (p == null) return;
            _custIconPath = p;
            SetIconChoice("__custom__");
            PreviewIcon();
        }
        private void PreviewIcon()
        {
            var src = !string.IsNullOrEmpty(_custIconPath) && File.Exists(_custIconPath) ? LoadImage(_custIconPath, 128) : null;
            _custIcon.Source = src ?? (Instance != null ? InstanceIcons.For(Instance, 64) : null);
        }
        private void LoadCustomization()
        {
            var inst = Instance!;
            _custName.Text = inst.Name;
            _custPath.Text = inst.BasePath;
            _custIconPath = !string.IsNullOrEmpty(inst.Icon) && File.Exists(inst.Icon) ? inst.Icon : "";
            SetIconChoice(_custIconPath.Length > 0 ? "__custom__" : "__default__");
            PreviewIcon();
        }
        private void SaveCustomization()
        {
            var inst = Instance!;
            var name = _custName.Text.Trim();
            if (name.Length == 0)
            {
                Error(T("QT_TAB_CUSTOMIZATION", "Customization"), T("QT_NAME_EMPTY", "Name cannot be empty."));
                return;
            }
            var other = InstanceManager.Instance.GetInstanceByName(name);
            if (other != null && other.InstanceId != inst.InstanceId)
            {
                Error(T("QT_TAB_CUSTOMIZATION", "Customization"), T("QT_NAME_TAKEN", "An instance called '{name}' already exists.", ("name", name)));
                return;
            }
            inst.Name = name;
            try { InstanceManager.Instance.SetIcon(inst, string.IsNullOrEmpty(_custIconPath) ? null : _custIconPath); }
            catch (Exception ex) { Error(T("QT_ICON", "Icon"), T("QT_ICON_FAIL", "Could not use that image: {error}", ("error", ex.Message))); }
            Save(T("QT_INSTANCE_SAVED", "Instance saved"));
            LoadCustomization();
        }

        private CheckBox _mvFullscreen = null!;
        private TextBox _mvWidth = null!, _mvHeight = null!;
        private Slider _mvMin = null!, _mvMax = null!, _mvPerm = null!;
        private CheckBox _mvGpu = null!;
        private TextBox _mvJvm = null!, _mvEnv = null!, _mvPre = null!, _mvWrap = null!, _mvPost = null!;
        private FrameworkElement SliderRow(string label, int lo, int hi, int step, string? zeroText, out Slider slider)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
            var head = new DockPanel();
            var value = Label("", "accent");
            DockPanel.SetDock(value, Dock.Right);
            head.Children.Add(value);
            head.Children.Add(Label(label));
            sp.Children.Add(head);
            var s = new Slider { Minimum = lo, Maximum = hi, TickFrequency = step, IsSnapToTickEnabled = true, SmallChange = step, LargeChange = Math.Max(step, 1024), Margin = new Thickness(0, 4, 0, 0) };
            void Fmt()
            {
                var v = (int)s.Value;
                value.Text = v == 0 && zeroText != null ? zeroText : (v % 1024 == 0 && v > 0 ? $"{v / 1024} GB" : $"{v} MB");
            }
            s.ValueChanged += (_, _) => Fmt();
            Fmt();
            sp.Children.Add(s);
            slider = s;
            return sp;
        }
        private FrameworkElement BuildMemVideo()
        {
            var (scroll, body) = Page(new Thickness(18, 16, 18, 16));
            var card = Card(T("QT_WINDOW", "Window"), out var cb);
            cb.Children.Add(ToggleRow(T("QT_FULLSCREEN", "Launch in fullscreen"), T("QT_FULLSCREEN_DESC", "Writes fullscreen:true into this instance's options.txt before every launch."), false, null, out _mvFullscreen));
            _mvWidth = TextBox("", "1920");
            _mvWidth.Width = 90;
            _mvHeight = TextBox("", "1080");
            _mvHeight.Width = 90;
            var res = Row(8, _mvWidth, Label("x"), _mvHeight, Label(T("QT_RESOLUTION_HINT", "(empty = Minecraft default)"), "hint"));
            cb.Children.Add(FormRow(T("QT_RESOLUTION", "Custom resolution"), res));
            body.Children.Add(card);
            var usable = SystemPerformanceHelper.GetMaxGameRamMB();
            card = Card(T("QT_MEMORY", "Memory"), out cb);
            cb.Children.Add(SliderRow(T("QT_MEM_MIN", "Minimum memory (Xms)"), 256, usable, 128, null, out _mvMin));
            cb.Children.Add(SliderRow(T("QT_MEM_MAX", "Maximum memory (Xmx)"), 512, usable, 256, null, out _mvMax));
            cb.Children.Add(SliderRow(T("QT_MEM_PERM", "PermGen / Metaspace"), 0, 2048, 64, T("QT_DEFAULT", "default"), out _mvPerm));
            cb.Children.Add(Label(T("QT_MEM_HINT", "Detected system memory: {gb} GB. Sliders stop 1 GB below that so the desktop keeps breathing.", ("gb", SystemPerformanceHelper.GetTotalRamGB())), "hint", true));
            body.Children.Add(card);
            card = Card(T("QT_GRAPHICS", "Graphics"), out cb);
            cb.Children.Add(ToggleRow(T("QT_GPU_INSTANCE", "Run on the high-performance GPU"), T("QT_GPU_INSTANCE_DESC", "Sets the Windows graphics preference of this instance's Java to High performance. For laptops with two GPUs."), false, null, out _mvGpu));
            body.Children.Add(card);
            card = Card(T("QT_VARIABLES", "Variables"), out cb);
            _mvJvm = TextBox("", null, true);
            cb.Children.Add(FormRow(T("QT_JVM_ARGS", "Java (JVM) arguments"), _mvJvm, T("QT_JVM_ARGS_HINT", "Appended after the memory flags. One line or space separated, e.g. -XX:+UseG1GC -Dsome.prop=1")));
            _mvEnv = TextBox("", null, true);
            cb.Children.Add(FormRow(T("QT_ENV_VARS", "Environment variables"), _mvEnv, T("QT_ENV_VARS_HINT", "One KEY=VALUE per line, lines starting with # are ignored.")));
            _mvPre = TextBox();
            cb.Children.Add(FormRow(T("QT_PRE_LAUNCH", "Pre-launch command"), _mvPre, T("QT_PRE_LAUNCH_HINT_WIN", "Runs in the .minecraft folder (cmd) before the game starts; a non-zero exit aborts the launch. %INST_NAME%, %INST_DIR%, %INST_MC_DIR%, %INST_JAVA%, %INST_MC_VER% are set.")));
            _mvWrap = TextBox();
            cb.Children.Add(FormRow(T("QT_WRAPPER", "Wrapper command"), _mvWrap, T("QT_WRAPPER_HINT_WIN", "Prepended to the java command line, e.g. a profiler or a custom starter executable.")));
            _mvPost = TextBox();
            cb.Children.Add(FormRow(T("QT_POST_EXIT", "Post-exit command"), _mvPost, T("QT_POST_EXIT_HINT_WIN", "Runs after Minecraft closes. %INST_EXIT_CODE% holds the exit code.")));
            body.Children.Add(card);
            body.Children.Add(SaveRow(SaveMemVideo));
            return scroll;
        }
        private void LoadMemVideo()
        {
            var inst = Instance!;
            var o = inst.Options;
            _mvFullscreen.IsChecked = o.Fullscreen;
            _mvWidth.Text = o.ResWidth > 0 ? o.ResWidth.ToString() : "";
            _mvHeight.Text = o.ResHeight > 0 ? o.ResHeight.ToString() : "";
            _mvMax.Value = Math.Clamp(inst.RamMb, _mvMax.Minimum, _mvMax.Maximum);
            _mvMin.Value = Math.Clamp(o.MinRamMb > 0 ? o.MinRamMb : Math.Min(512, inst.RamMb), _mvMin.Minimum, _mvMin.Maximum);
            _mvPerm.Value = Math.Clamp(o.PermGenMb, 0, 2048);
            _mvGpu.IsChecked = o.DiscreteGpu;
            _mvJvm.Text = o.JvmArgs;
            _mvEnv.Text = inst.EnvVars;
            _mvPre.Text = o.PreLaunchCmd;
            _mvWrap.Text = o.WrapperCmd;
            _mvPost.Text = o.PostExitCmd;
        }
        private void SaveMemVideo()
        {
            var inst = Instance!;
            var o = inst.Options;
            var w = _mvWidth.Text.Trim();
            var h = _mvHeight.Text.Trim();
            if ((w.Length > 0 || h.Length > 0) && !(int.TryParse(w, out var wi) && int.TryParse(h, out var hi) && wi > 0 && hi > 0))
            {
                Error(T("QT_RESOLUTION", "Custom resolution"), T("QT_RESOLUTION_INVALID", "Width and height must both be positive numbers, e.g. 1920 x 1080."));
                return;
            }
            o.Fullscreen = _mvFullscreen.IsChecked == true;
            o.ResWidth = int.TryParse(w, out var rw) ? rw : 0;
            o.ResHeight = int.TryParse(h, out var rh) ? rh : 0;
            var max = (int)_mvMax.Value;
            inst.RamMb = max;
            o.MinRamMb = Math.Min((int)_mvMin.Value, max);
            o.PermGenMb = (int)_mvPerm.Value;
            o.DiscreteGpu = _mvGpu.IsChecked == true;
            o.JvmArgs = _mvJvm.Text.Trim();
            inst.EnvVars = _mvEnv.Text.Trim();
            o.PreLaunchCmd = _mvPre.Text.Trim();
            o.WrapperCmd = _mvWrap.Text.Trim();
            o.PostExitCmd = _mvPost.Text.Trim();
            if (Save(T("QT_MEM_VIDEO_SAVED", "Memory & video settings saved")))
            {
                try { LaunchService.ApplyVideoOptions(inst); InstanceManager.Instance.SaveInstances(); }
                catch (Exception ex) { _launcher.AppendLog($"[Editor] options.txt apply failed: {ex.Message}"); }
            }
        }

        private TextBlock _mpInfo = null!;
        private Button _mpCheck = null!, _mpOpen = null!, _mpRepair = null!, _mpUnlink = null!;
        private Border _mpVersionsCard = null!;
        private ListBox _mpVersions = null!;
        private List<ModrinthVersion> _mpVersionList = new();
        private FrameworkElement BuildModpack()
        {
            var (scroll, body) = Page(new Thickness(18, 16, 18, 16));
            var card = Card(T("QT_MODPACK", "Modpack"), out var cb);
            _mpInfo = Label("", "", true);
            cb.Children.Add(_mpInfo);
            _mpCheck = Button(T("QT_CHECK_UPDATES", "Check for updates"), MpCheckUpdates, "", "update");
            _mpOpen = Button(T("QT_OPEN_MODRINTH", "Open on Modrinth"), MpOpenPage, "", "github");
            _mpRepair = Button(T("QT_REPAIR", "Repair (re-download missing files)"), MpRepair, "", "refresh");
            _mpUnlink = Button(T("QT_UNLINK", "Unlink pack"), MpUnlink, "", "trash");
            var bar = Bar(_mpCheck, _mpOpen, _mpRepair, _mpUnlink, Button(T("QT_IMPORT_INTO", "Import a different .mrpack / .orangpack into this instance"), MpImportInto, "", "mrpack"));
            bar.Margin = new Thickness(0, 12, 0, 0);
            cb.Children.Add(bar);
            body.Children.Add(card);
            _mpVersionsCard = Card(T("QT_AVAILABLE_VERSIONS", "Available versions"), out var cb2);
            cb2.Children.Add(Label(T("QT_MP_VERSIONS_HINT", "Pick a version and press Update. Mods from the old version are removed, your overrides and worlds stay."), "hint", true));
            _mpVersions = new ListBox { MinHeight = 180, MaxHeight = 360, Margin = new Thickness(0, 8, 0, 8) };
            cb2.Children.Add(_mpVersions);
            cb2.Children.Add(Bar(Button(T("QT_UPDATE_TO_SELECTED", "Update to selected version"), MpUpdateSelected, "accent")));
            body.Children.Add(_mpVersionsCard);
            return scroll;
        }
        private void LoadModpack()
        {
            var meta = ModpackMeta.Load(Instance!);
            _mpVersions.Items.Clear();
            _mpVersionList.Clear();
            var linked = meta != null;
            var onModrinth = !string.IsNullOrEmpty(meta?.ModrinthProjectId);
            _mpUnlink.Visibility = _mpRepair.Visibility = linked ? Visibility.Visible : Visibility.Collapsed;
            _mpCheck.Visibility = _mpOpen.Visibility = _mpVersionsCard.Visibility = onModrinth ? Visibility.Visible : Visibility.Collapsed;
            if (meta == null)
            {
                _mpInfo.Text = T("QT_MP_NOT_FROM_PACK", "This instance was not created from a modpack.\nImport an .mrpack or .orangpack below to link one.");
                return;
            }
            var lines = new List<string>
            {
                T("QT_MP_NAME", "Name: {name}", ("name", meta.ModrinthTitle ?? meta.Name ?? "?")),
                T("QT_MP_INSTALLED_VERSION", "Installed version: {version}", ("version", meta.ModrinthVersionNumber ?? meta.VersionId ?? "?")),
                T("QT_MP_FILE_COUNT", "Files managed by the pack: {n}", ("n", meta.FileCount)),
                T("QT_MP_IMPORTED", "Imported: {date}", ("date", (meta.Imported ?? "").Replace('T', ' ')))
            };
            if (!string.IsNullOrEmpty(meta.Summary)) lines.Add(T("QT_MP_SUMMARY", "Summary: {summary}", ("summary", meta.Summary)));
            lines.Add(onModrinth
                ? T("QT_MP_PROJECT", "Modrinth project: {project}", ("project", meta.ModrinthSlug ?? meta.ModrinthProjectId))
                : T("QT_MP_NOT_LINKED", "Not linked to a Modrinth project (imported from a local file)."));
            _mpInfo.Text = string.Join("\n", lines);
        }
        private void MpOpenPage()
        {
            var meta = ModpackMeta.Load(Instance!);
            var slug = meta?.ModrinthSlug ?? meta?.ModrinthProjectId;
            if (slug != null) OpenUrl($"https://modrinth.com/modpack/{slug}");
            else Info(T("QT_MODPACK", "Modpack"), T("QT_MP_NOT_ON_MODRINTH", "This instance is not linked to a Modrinth project."));
        }
        private async void MpCheckUpdates()
        {
            var meta = ModpackMeta.Load(Instance!);
            var pid = meta?.ModrinthProjectId;
            if (pid == null)
            {
                Info(T("QT_MODPACK", "Modpack"), T("QT_MP_NOT_ON_MODRINTH", "This instance is not linked to a Modrinth project."));
                return;
            }
            _mpVersions.Items.Clear();
            _mpVersions.Items.Add(T("QT_LOADING_VERSIONS", "Loading versions..."));
            try
            {
                var versions = (await ModrinthClient.GetVersionsAsync(pid)).OrderByDescending(v => v.DatePublished).ToList();
                _mpVersions.Items.Clear();
                _mpVersionList = versions;
                foreach (var v in versions)
                {
                    var tag = v.Id == meta!.ModrinthVersionId ? $"  ({T("QT_INSTALLED_TAG", "installed")})" : "";
                    _mpVersions.Items.Add($"{v.VersionNumber}  -  MC {string.Join(", ", v.GameVersions.Take(3))}  -  {string.Join(", ", v.Loaders)}  -  {v.DatePublished:yyyy-MM-dd}{tag}");
                }
                if (versions.Count > 0 && versions[0].Id != meta!.ModrinthVersionId)
                    _mpInfo.Text += "\n" + T("QT_MP_UPDATE_AVAILABLE", "Update available: {version}", ("version", versions[0].VersionNumber));
            }
            catch (Exception ex)
            {
                _mpVersions.Items.Clear();
                Error(T("QT_MODPACK", "Modpack"), T("QT_MP_FETCH_FAIL", "Could not fetch versions: {error}", ("error", ex.Message)));
            }
        }
        private void MpUpdateSelected()
        {
            var row = _mpVersions.SelectedIndex;
            if (row < 0 || row >= _mpVersionList.Count)
            {
                Info(T("QT_MODPACK", "Modpack"), T("QT_MP_PICK_FIRST", "Check for updates first and pick a version from the list."));
                return;
            }
            var version = _mpVersionList[row];
            var file = version.PrimaryFile;
            if (file == null)
            {
                Error(T("QT_MODPACK", "Modpack"), T("QT_MP_NO_FILE", "That version has no downloadable file."));
                return;
            }
            if (!Ask(T("QT_UPDATE_MODPACK", "Update modpack"), T("QT_MP_UPDATE_CONFIRM", "Update '{name}' to {version}?\n\nManaged mods from the current version are removed first, everything else stays.", ("name", Instance!.Name), ("version", version.VersionNumber))))
                return;
            MpApply(file.Url, file.Filename, true, null);
        }
        private async void MpApply(string? url, string? filename, bool removeManaged, string? localPath)
        {
            var inst = Instance!;
            _launcher.SetBusy(true, T("QT_UPDATING_MODPACK", "Updating modpack..."));
            try
            {
                var path = localPath;
                if (path == null)
                {
                    path = Path.Combine(Path.GetTempPath(), "orang_pack_" + Guid.NewGuid().ToString("N"), filename ?? "pack.mrpack");
                    await ModrinthClient.DownloadFileAsync(url!, path);
                }
                var importer = new ModpackImporter();
                importer.ProgressChanged += (t, p) => _launcher.SetProgress((int)p, t);
                var (ok, msg) = await Task.Run(() => importer.ApplyToInstanceAsync(path, inst, removeManaged));
                _launcher.SetBusy(false, T("QT_READY", "Ready"));
                if (ok) Info(T("QT_MODPACK", "Modpack"), msg); else Error(T("QT_MODPACK", "Modpack"), msg);
            }
            catch (Exception ex)
            {
                _launcher.SetBusy(false, T("QT_READY", "Ready"));
                Error(T("QT_MODPACK", "Modpack"), ex.Message);
            }
            LoadModpack();
            RefreshHeader();
            _loadedTabs.Remove("mods");
            _loadedTabs.Remove("packs");
            _loadedTabs.Remove("versions");
        }
        private async void MpRepair()
        {
            var meta = ModpackMeta.Load(Instance!);
            var src = meta?.SourceFile;
            if (!string.IsNullOrEmpty(src) && File.Exists(src))
            {
                if (Ask(T("QT_REPAIR_TITLE", "Repair"), T("QT_REPAIR_CONFIRM", "Re-download missing pack files using\n{path}?", ("path", src))))
                    MpApply(null, null, false, src);
                return;
            }
            if (string.IsNullOrEmpty(meta?.ModrinthVersionId))
            {
                Info(T("QT_REPAIR_TITLE", "Repair"), T("QT_REPAIR_NO_SOURCE", "The original pack file is gone and the instance is not linked to Modrinth.\nImport the pack file again to repair."));
                return;
            }
            try
            {
                var version = await ModrinthClient.GetVersionAsync(meta.ModrinthVersionId!);
                var file = version?.PrimaryFile;
                if (file != null) MpApply(file.Url, file.Filename, false, null);
            }
            catch (Exception ex) { Error(T("QT_REPAIR_TITLE", "Repair"), ex.Message); }
        }
        private void MpUnlink()
        {
            var p = ModpackMeta.PathFor(Instance!);
            if (File.Exists(p) && Ask(T("QT_UNLINK_TITLE", "Unlink"), T("QT_UNLINK_CONFIRM", "Forget the modpack link? Files stay, only update tracking is removed.")))
            {
                File.Delete(p);
                LoadModpack();
            }
        }
        private void MpImportInto()
        {
            var path = PickFile(T("QT_IMPORT_INTO_TITLE", "Import modpack into this instance"), Filter((T("QT_FT_MODPACKS", "Modpacks"), "*.mrpack;*.orangpack")) + "|" + AllFiles);
            if (path == null) return;
            if (Ask(T("QT_IMPORT", "Import"), T("QT_IMPORT_INTO_CONFIRM", "Apply this pack on top of the current instance?\nManaged mods of the previously linked pack are removed first.")))
                MpApply(null, null, true, path);
        }

        private TreeView _shTree = null!;
        private RadioButton _fmtOrang = null!, _fmtMr = null!, _fmtZip = null!;
        private FrameworkElement BuildSharing()
        {
            var (scroll, body) = Page(new Thickness(18, 16, 18, 16));
            var card = Card(T("QT_EXPORT", "Export"), out var cb);
            cb.Children.Add(Label(T("QT_EXPORT_HINT", "Tick what to include, choose a format, export. Files that exist on Modrinth are referenced by download link instead of being packed, which keeps .mrpack and .orangpack small."), "hint", true));
            _fmtOrang = new RadioButton { Content = T("QT_FMT_ORANGPACK", "OrangLauncher pack (.orangpack, smallest, keeps instance settings)"), IsChecked = true, GroupName = "fmt", Margin = new Thickness(0, 8, 0, 2) };
            _fmtMr = new RadioButton { Content = T("QT_FMT_MRPACK", "Modrinth pack (.mrpack, works in every launcher)"), GroupName = "fmt" };
            _fmtZip = new RadioButton { Content = T("QT_FMT_ZIP", "Full zip (.zip, everything copied, biggest)"), GroupName = "fmt" };
            cb.Children.Add(_fmtOrang);
            cb.Children.Add(_fmtMr);
            cb.Children.Add(_fmtZip);
            _shTree = new TreeView { MinHeight = 320, MaxHeight = 520, Margin = new Thickness(0, 10, 0, 10), BorderThickness = new Thickness(1) };
            _shTree.Background = Brushes.Transparent;
            _shTree.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            cb.Children.Add(_shTree);
            cb.Children.Add(Bar(
                Button(T("QT_REFRESH_LIST", "Refresh list"), LoadSharing, "", "refresh"),
                Button(T("SELECT_ALL", "Select all"), () => SetAll(true)),
                Button(T("QT_SELECT_NONE", "Select none"), () => SetAll(false)),
                Button(T("QT_EXPORT_BTN", "Export..."), Export, "accent")));
            body.Children.Add(card);
            return scroll;
        }
        private sealed class ShNode
        {
            public string Rel = "";
            public bool IsDir;
            public CheckBox Box = null!;
            public TreeViewItem Item = null!;
            public List<ShNode> Children = new();
            public ShNode? Parent;
        }
        private readonly List<ShNode> _shRoots = new();
        private ShNode MakeNode(ShNode? parent, string text, string rel, bool check, bool isDir)
        {
            var node = new ShNode { Rel = rel, IsDir = isDir, Parent = parent };
            node.Box = new CheckBox { Content = text, IsChecked = check, IsThreeState = false, Margin = new Thickness(0, 2, 0, 2) };
            node.Box.SetResourceReference(FrameworkElement.StyleProperty, "OlTickBox");
            node.Item = new TreeViewItem { Header = node.Box };
            node.Item.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush");
            node.Box.Click += (_, _) =>
            {
                SetRecursive(node, node.Box.IsChecked == true);
                UpdateParents(node.Parent);
            };
            if (parent == null) { _shRoots.Add(node); _shTree.Items.Add(node.Item); }
            else { parent.Children.Add(node); parent.Item.Items.Add(node.Item); }
            return node;
        }
        private static void SetRecursive(ShNode n, bool value)
        {
            n.Box.IsThreeState = false;
            n.Box.IsChecked = value;
            foreach (var c in n.Children) SetRecursive(c, value);
        }
        private static void UpdateParents(ShNode? n)
        {
            while (n != null)
            {
                var all = n.Children.All(c => c.Box.IsChecked == true);
                var none = n.Children.All(c => c.Box.IsChecked == false);
                n.Box.IsThreeState = !all && !none;
                n.Box.IsChecked = all ? true : none ? false : null;
                n = n.Parent;
            }
        }
        private void SetAll(bool value)
        {
            foreach (var r in _shRoots) SetRecursive(r, value);
        }
        private void LoadSharing()
        {
            _shTree.Items.Clear();
            _shRoots.Clear();
            var baseDir = Instance!.BasePath;
            if (!Directory.Exists(baseDir)) return;
            void AddDir(ShNode? parent, DirectoryInfo dir, string rel, int depth)
            {
                var node = MakeNode(parent, dir.Name, rel, !InstanceExporter.IsSkipped(rel), true);
                if (depth >= 2) return;
                FileSystemInfo[] children;
                try { children = dir.GetFileSystemInfos().OrderBy(x => x is FileInfo).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Take(400).ToArray(); }
                catch { return; }
                foreach (var c in children)
                {
                    if (FileLinks.IsLink(c.FullName)) continue;
                    var crel = $"{rel}/{c.Name}";
                    if (c is DirectoryInfo d) AddDir(node, d, crel, depth + 1);
                    else MakeNode(node, c.Name, crel, !InstanceExporter.IsSkipped(rel) && !InstanceExporter.IsSkipped(crel), false);
                }
                UpdateParents(node);
            }
            var root = new DirectoryInfo(baseDir);
            foreach (var e in root.GetFileSystemInfos().OrderBy(x => x is FileInfo).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (FileLinks.IsLink(e.FullName)) continue;
                if (e is DirectoryInfo d)
                {
                    AddDir(null, d, d.Name, 0);
                    _shRoots[^1].Item.IsExpanded = true;
                }
                else MakeNode(null, e.Name, e.Name, e.Name is "icon.png" or "modpack.json", false);
            }
        }
        private List<string> SelectedPaths()
        {
            var baseDir = Instance!.BasePath;
            var result = new HashSet<string>();
            void Walk(ShNode n)
            {
                if (n.Children.Count > 0)
                {
                    foreach (var c in n.Children) Walk(c);
                    if (n.IsDir && n.Box.IsChecked == true)
                    {
                        var listed = n.Children.Select(c => c.Rel).ToHashSet();
                        var depth = n.Rel.Count(ch => ch == '/') + 2;
                        foreach (var f in Directory.EnumerateFiles(Path.Combine(baseDir, n.Rel), "*", SearchOption.AllDirectories))
                        {
                            var rel = Path.GetRelativePath(baseDir, f).Replace('\\', '/');
                            var first = string.Join("/", rel.Split('/').Take(depth));
                            if (!listed.Contains(first)) result.Add(rel);
                        }
                    }
                }
                else if (n.Box.IsChecked == true)
                {
                    var full = Path.Combine(baseDir, n.Rel);
                    if (Directory.Exists(full))
                    {
                        foreach (var f in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
                            result.Add(Path.GetRelativePath(baseDir, f).Replace('\\', '/'));
                    }
                    else result.Add(n.Rel);
                }
            }
            foreach (var r in _shRoots) Walk(r);
            return result.OrderBy(x => x).ToList();
        }
        private async void Export()
        {
            var inst = Instance!;
            var fmt = _fmtMr.IsChecked == true ? "mrpack" : _fmtZip.IsChecked == true ? "zip" : "orangpack";
            var ext = fmt == "mrpack" ? ".mrpack" : fmt == "zip" ? ".zip" : ModpackImporter.OrangpackExt;
            var safe = Regex.Replace(inst.Name, @"[^\w.-]+", "_").Trim('_');
            if (safe.Length == 0) safe = "instance";
            var path = SaveFile(T("QT_EXPORT_NAME", "Export {name}", ("name", inst.Name)), $"{ext[1..]} (*{ext})|*{ext}", safe + ext);
            if (path == null) return;
            if (!path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) path += ext;
            var selected = SelectedPaths();
            if (selected.Count == 0)
            {
                Info(T("QT_EXPORT", "Export"), T("QT_NOTHING_SELECTED", "Nothing selected."));
                return;
            }
            _launcher.SetStatus(T("QT_EXPORTING", "Exporting..."));
            try
            {
                var outPath = await Task.Run(() => InstanceExporter.ExportAsync(inst, path, fmt, selected, _launcher.AppendLog,
                    (i, t, n) => _launcher.SetProgress((int)(i * 100.0 / Math.Max(1, t)), T("QT_EXPORTING_FILE", "Exporting {name}", ("name", n)))));
                _launcher.SetProgress(0, T("QT_READY", "Ready"));
                Info(T("QT_EXPORT", "Export"), T("QT_EXPORTED_TO", "Exported to:\n{path}", ("path", outPath)));
            }
            catch (Exception ex)
            {
                _launcher.SetProgress(0, T("QT_READY", "Ready"));
                Error(T("QT_EXPORT_FAILED", "Export failed"), ex.Message);
            }
        }

        private ListBox _ssList = null!;
        private TextBlock _ssCount = null!;
        private int _ssToken;
        private FrameworkElement BuildScreenshots()
        {
            var dock = new DockPanel { Margin = new Thickness(14, 12, 14, 12) };
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var buttons = Row(8,
                Button(T("QT_REFRESH", "Refresh"), LoadScreenshots, "", "refresh"),
                Button(T("RES_SH_OPEN_FOLDER", "Open folder"), () => { if (Instance != null) { Directory.CreateDirectory(ScreenshotDir()); OpenPath(ScreenshotDir()); } }, "", "folder"),
                Button(T("QT_COPY_PICTURES_WIN", "Copy selected to Pictures"), CopyScreenshots, "", "download"),
                Button(T("QT_DELETE_SELECTED", "Delete selected"), DeleteScreenshots, "", "trash"));
            DockPanel.SetDock(buttons, Dock.Right);
            head.Children.Add(buttons);
            _ssCount = Label("", "muted");
            _ssCount.Margin = new Thickness(10, 0, 0, 0);
            head.Children.Add(Row(0, Label(T("QT_TAB_SCREENSHOTS", "Screenshots"), "h2"), _ssCount));
            DockPanel.SetDock(head, Dock.Top);
            dock.Children.Add(head);
            _ssList = new ListBox { SelectionMode = SelectionMode.Extended };
            _ssList.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));
            _ssList.MouseDoubleClick += (_, _) => { if (_ssList.SelectedItem is FrameworkElement { Tag: string p }) OpenInEditor(p); };
            dock.Children.Add(_ssList);
            return dock;
        }
        private string ScreenshotDir() => FileLinks.Resolve(Instance!.ScreenshotsDir);
        private async void LoadScreenshots()
        {
            if (Instance == null) return;
            _ssList.Items.Clear();
            var dir = ScreenshotDir();
            var pngs = Directory.Exists(dir) ? new DirectoryInfo(dir).GetFiles("*.png").OrderByDescending(f => f.LastWriteTime).ToList() : new List<FileInfo>();
            _ssCount.Text = T("QT_SCREENSHOTS_COUNT", "{n} screenshot(s)", ("n", pngs.Count));
            if (pngs.Count == 0)
            {
                _ssList.Items.Add(Label(T("QT_NO_SCREENSHOTS", "No screenshots yet. F2 in game takes one."), "muted"));
                return;
            }
            var token = ++_ssToken;
            foreach (var png in pngs.Take(200))
            {
                var img = await Task.Run(() => LoadImage(png.FullName, 180));
                if (token != _ssToken) return;
                if (img == null) continue;
                var sp = new StackPanel { Width = 190, Margin = new Thickness(4), Tag = png.FullName, ToolTip = png.Name };
                sp.Children.Add(new Image { Source = img, Height = 110, Stretch = Stretch.Uniform });
                var name = Label(png.Name.Length > 26 ? png.Name[..26] : png.Name, "hint");
                name.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Children.Add(name);
                _ssList.Items.Add(sp);
            }
        }
        private List<string> SelectedScreenshots() => _ssList.SelectedItems.OfType<FrameworkElement>().Select(f => f.Tag as string).Where(p => p != null).Select(p => p!).ToList();
        private void CopyScreenshots()
        {
            var sel = SelectedScreenshots();
            if (sel.Count == 0)
            {
                Info(T("QT_TAB_SCREENSHOTS", "Screenshots"), T("QT_SELECT_SCREENSHOTS_FIRST", "Click screenshots to select them first."));
                return;
            }
            var dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Minecraft", Regex.Replace(Instance!.Name, @"[^\w.-]+", "_"));
            Directory.CreateDirectory(dest);
            int n = 0;
            foreach (var p in sel)
            {
                try { File.Copy(p, Path.Combine(dest, Path.GetFileName(p)), true); n++; } catch { }
            }
            Info(T("QT_TAB_SCREENSHOTS", "Screenshots"), T("QT_COPIED_FILES_TO", "Copied {n} file(s) to\n{path}", ("n", n), ("path", dest)));
        }
        private void DeleteScreenshots()
        {
            var sel = SelectedScreenshots();
            if (sel.Count == 0)
            {
                Info(T("QT_TAB_SCREENSHOTS", "Screenshots"), T("QT_SELECT_SCREENSHOTS_FIRST", "Click screenshots to select them first."));
                return;
            }
            if (!Ask(T("QT_DELETE_SCREENSHOTS", "Delete screenshots"), T("QT_DELETE_SCREENSHOTS_CONFIRM", "Delete {n} screenshot(s)? This cannot be undone.", ("n", sel.Count)))) return;
            foreach (var p in sel) { try { File.Delete(p); } catch { } }
            LoadScreenshots();
        }

        private CheckBox _otLegacy = null!, _otAnti = null!, _otDemo = null!;
        private FrameworkElement BuildOther()
        {
            var (scroll, body) = Page(new Thickness(18, 16, 18, 16));
            var card = Card(T("QT_COMPAT", "Compatibility"), out var cb);
            cb.Children.Add(ToggleRow(T("QT_LEGACY_FIXES", "Legacy client fixes (old Minecraft versions)"),
                T("QT_LEGACY_FIXES_DESC_WIN", "Adds the JVM flags old clients need (legacy merge sort, relaxed FML certificate checks). Only affects versions below 1.13."), false, null, out _otLegacy));
            cb.Children.Add(ToggleRow(T("QT_ANTI_BAN", "Bypass Mojang's blocked-server list"),
                T("QT_ANTI_BAN_DESC", "Removes Mojang's server block list provider from the game's classpath, so servers on the secret sessionserver.mojang.com/blockedservers list can still be joined. Works on 1.16.4 and newer. Turn it off if a server refuses the connection."), true, null, out _otAnti));
            cb.Children.Add(ToggleRow(T("QT_DEMO_MODE", "Launch the demo version (--demo)"),
                T("QT_DEMO_MODE_DESC", "Passes --demo to the game. Use this if you want to try the Java Edition demo: it starts the demo world with the 5 in-game day limit, no account purchase needed."), false, null, out _otDemo));
            body.Children.Add(card);
            var card2 = Card(T("QT_DANGER", "Danger zone"), out var cb2);
            cb2.Children.Add(Bar(
                Button(T("QT_DELETE_INSTANCE", "Delete this instance"), () => { if (Instance != null && _launcher.DeleteInstance(Instance)) _back(); }, "danger"),
                Button(T("GAME_PROFILES_DUPLICATE", "Duplicate"), () =>
                {
                    if (Instance == null) return;
                    _launcher.DuplicateInstance(Instance);
                    _back();
                }, "", "dublicate")));
            body.Children.Add(card2);
            body.Children.Add(SaveRow(SaveOther));
            return scroll;
        }
        private void LoadOther()
        {
            var o = Instance!.Options;
            _otLegacy.IsChecked = o.LegacyFixes;
            _otAnti.IsChecked = o.AntiBan;
            _otDemo.IsChecked = o.Demo;
        }
        private void SaveOther()
        {
            var o = Instance!.Options;
            o.LegacyFixes = _otLegacy.IsChecked == true;
            o.AntiBan = _otAnti.IsChecked == true;
            o.Demo = _otDemo.IsChecked == true;
            Save(T("QT_OTHER_SAVED", "Other settings saved"));
        }

        private McVersionCombo _vrMc = null!;
        private ComboBox _vrLoader = null!, _vrLoaderVer = null!;
        private JavaCombo _vrJava = null!;
        private TextBlock _vrStatus = null!, _vrInstalled = null!;
        private bool _vrLoading;
        private int _vrToken;
        private FrameworkElement BuildVersions()
        {
            var (scroll, body) = Page(new Thickness(18, 16, 18, 16));
            var card = Card(T("QT_COMPONENTS", "Components"), out var cb);
            var form = Form(150);
            _vrMc = new McVersionCombo { MaxWidth = 360, HorizontalAlignment = HorizontalAlignment.Left };
            _vrMc.VersionChanged += _ => VrLoaderVersions();
            FormAdd(form, T("QT_MINECRAFT", "Minecraft"), _vrMc);
            _vrLoader = Combo();
            _vrLoader.MaxWidth = 360;
            _vrLoader.HorizontalAlignment = HorizontalAlignment.Left;
            _vrLoader.MinWidth = 180;
            foreach (var l in LoaderVersionService.Loaders) _vrLoader.Items.Add(l);
            _vrLoader.SelectionChanged += (_, _) => VrLoaderVersions();
            FormAdd(form, T("QT_MOD_LOADER", "Mod loader"), _vrLoader);
            _vrLoaderVer = Combo();
            _vrLoaderVer.MaxWidth = 360;
            _vrLoaderVer.MinWidth = 180;
            _vrLoaderVer.HorizontalAlignment = HorizontalAlignment.Left;
            FormAdd(form, T("QT_LOADER_VERSION", "Loader version"), _vrLoaderVer);
            _vrJava = new JavaCombo { MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
            FormAdd(form, T("QT_JAVA_RUNTIME", "Java runtime"), _vrJava);
            cb.Children.Add(form);
            cb.Children.Add(Label(T("QT_JAVA_RUNTIME_HINT", "Automatic picks the Java version this Minecraft release needs (8, 17 or 21) and downloads it when missing."), "hint", true));
            _vrStatus = Label("", "hint", true);
            _vrStatus.Margin = new Thickness(0, 6, 0, 0);
            cb.Children.Add(_vrStatus);
            body.Children.Add(card);
            body.Children.Add(Bar(
                Button(T("QT_SAVE", "Save"), SaveVersions, "accent"),
                Button(T("QT_REINSTALL", "Reinstall game files"), Reinstall, "", "refresh"),
                Button(T("QT_OPEN_VERSIONS", "Open versions folder"), () => OpenPath(Path.Combine(PlatformPaths.GetMinecraftDir(), "versions")), "", "folder")));
            var inst = Label(T("QT_INSTALLED", "Installed"), "h3");
            inst.Margin = new Thickness(0, 12, 0, 4);
            body.Children.Add(inst);
            _vrInstalled = Label("", "muted", true);
            body.Children.Add(_vrInstalled);
            return scroll;
        }
        private async void VrLoaderVersions()
        {
            if (_vrLoading || Instance == null) return;
            var loader = _vrLoader.SelectedItem as string ?? "vanilla";
            var mc = _vrMc.Version;
            _vrLoaderVer.Items.Clear();
            if (loader == "vanilla")
            {
                _vrLoaderVer.Items.Add("N/A");
                _vrLoaderVer.SelectedIndex = 0;
                _vrLoaderVer.IsEnabled = false;
                return;
            }
            _vrLoaderVer.IsEnabled = true;
            _vrLoaderVer.Items.Add(T("QT_LOADING", "Loading..."));
            _vrLoaderVer.SelectedIndex = 0;
            var current = Instance.LoaderVersion ?? "";
            var token = ++_vrToken;
            try
            {
                var versions = await LoaderVersionService.FetchAsync(loader, mc);
                if (token != _vrToken) return;
                _vrLoaderVer.Items.Clear();
                if (versions.Count > 0)
                {
                    foreach (var v in versions) _vrLoaderVer.Items.Add(v);
                    _vrLoaderVer.SelectedItem = versions.Contains(current) ? current : versions[0];
                }
                else
                {
                    _vrLoaderVer.Items.Add(T("LOADER_NOT_COMPATIBLE", "Not available for this version"));
                    _vrLoaderVer.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                if (token != _vrToken) return;
                _vrLoaderVer.Items.Clear();
                Error(T("QT_LOADER_VERSION", "Loader version"), T("QT_LOADER_FETCH_FAIL", "Could not load {loader} versions: {error}", ("loader", loader), ("error", ex.Message)));
            }
        }
        private void LoadVersions()
        {
            var inst = Instance!;
            _vrLoading = true;
            try
            {
                _vrMc.Select(inst.Version);
                _vrLoader.SelectedItem = inst.LoaderKey;
                _vrJava.Fill(inst.JavaPath);
            }
            finally { _vrLoading = false; }
            VrLoaderVersions();
            _vrStatus.Text = T("QT_JAVA_REQUIRED", "Java required: {major}", ("major", JavaResolver.DownloadableMajor(JavaResolver.RequiredJavaFor(inst.Version))));
            var installed = string.IsNullOrEmpty(inst.InstalledVersionName) ? (inst.IsVanilla ? inst.Version : T("QT_NOTHING_INSTALLED", "nothing installed yet (installs on first launch)")) : inst.InstalledVersionName;
            var versionsDir = Path.Combine(PlatformPaths.GetMinecraftDir(), "versions");
            var present = Directory.Exists(versionsDir)
                ? Directory.GetDirectories(versionsDir).Select(Path.GetFileName).Where(n => n != null && (n == inst.Version || n.Contains(inst.Version))).OrderBy(n => n).ToList()
                : new List<string?>();
            _vrInstalled.Text = T("QT_LAUNCH_ID", "Launch id: {id}", ("id", installed)) + "\n" +
                T("QT_VERSION_FOLDERS", "Version folders: {folders}", ("folders", present.Count > 0 ? string.Join(", ", present) : T("QT_NONE_DETECTED", "none")));
        }
        private void SaveVersions()
        {
            var inst = Instance!;
            var mc = _vrMc.Version.Trim();
            var loader = _vrLoader.SelectedItem as string ?? "vanilla";
            if (!VersionService.IsKnown(mc))
            {
                var s = VersionService.Suggestions(mc);
                Error(T("QT_UNKNOWN_VERSION", "Unknown version"), T("QT_UNKNOWN_VERSION_MSG", "'{version}' is not a valid Minecraft version.", ("version", mc)) +
                    (s.Count > 0 ? "\n\n" + T("QT_DID_YOU_MEAN", "Did you mean: {options}?", ("options", string.Join(", ", s))) : ""));
                return;
            }
            var lv = _vrLoaderVer.SelectedItem as string ?? "";
            var notCompat = T("LOADER_NOT_COMPATIBLE", "Not available for this version");
            if (loader != "vanilla" && lv == notCompat)
            {
                Error(T("LOADER_NOT_COMPATIBLE_TITLE", "Loader not available"), T("LOADER_NOT_COMPATIBLE_MSG", "{loader} has no build for Minecraft {version}.", ("loader", loader), ("version", mc)));
                return;
            }
            if (lv == "N/A" || lv == "Latest" || lv == T("QT_LOADING", "Loading...") || lv == notCompat || loader == "vanilla") lv = "";
            var changed = inst.Version != mc || inst.LoaderKey != loader || (inst.LoaderVersion ?? "") != lv;
            inst.Version = mc;
            inst.ModLoader = loader;
            inst.LoaderVersion = lv;
            if (changed) inst.InstalledVersionName = null;
            inst.JavaPath = _vrJava.JavaPath;
            if (Save(T("QT_VERSIONS_SAVED", "Versions saved"))) LoadVersions();
        }
        private void Reinstall()
        {
            var inst = Instance!;
            if (!Ask(T("QT_REINSTALL_TITLE", "Reinstall"), T("QT_REINSTALL_CONFIRM", "Delete the installed version folders (not mods, worlds or configs) so they are downloaded again on the next launch?")))
                return;
            var versionsDir = Path.Combine(PlatformPaths.GetMinecraftDir(), "versions");
            try
            {
                foreach (var name in new[] { inst.InstalledVersionName, inst.Version }.Where(n => !string.IsNullOrEmpty(n)).Distinct())
                {
                    var d = Path.Combine(versionsDir, name!);
                    if (Directory.Exists(d)) Directory.Delete(d, true);
                }
            }
            catch (Exception ex)
            {
                Error(T("QT_REINSTALL_TITLE", "Reinstall"), ex.Message);
                return;
            }
            inst.InstalledVersionName = null;
            Save(T("QT_REINSTALL_DONE", "Version files removed, they will be reinstalled on launch"));
            LoadVersions();
        }
    }
}