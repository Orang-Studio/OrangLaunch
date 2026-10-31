using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrangLauncher.Managers;
using OrangLauncher.Models;
using static OrangLauncher.UiKit;
namespace OrangLauncher.Pages
{
    public class ModsPanel : DockPanel
    {
        private readonly MainWindow _launcher;
        private MinecraftInstance? _instance;
        private readonly TextBlock _info = Label("", "h3");
        private readonly TextBlock _count = Label("", "muted");
        private readonly TextBox _search;
        private readonly TextBlock _warning;
        private readonly ListView _list = new() { SelectionMode = SelectionMode.Extended };
        private readonly TextBlock _selectedInfo;
        private int _metaToken;
        private sealed class ModRow
        {
            public string Path { get; init; } = "";
            public string Name { get; set; } = "";
            public string Version { get; set; } = "";
            public string File => System.IO.Path.GetFileName(Path);
            public bool Disabled => Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            public double Opacity => Disabled ? 0.45 : 1;
            public string? Tip { get; set; }
        }
        public ModsPanel(MainWindow launcher)
        {
            _launcher = launcher;
            Margin = new Thickness(14, 12, 14, 12);
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            _search = TextBox("", T("QT_FILTER_MODS", "Filter mods..."));
            _search.Width = 220;
            _search.TextChanged += (_, _) => Refresh();
            DockPanel.SetDock(_search, Dock.Right);
            head.Children.Add(_search);
            _count.Margin = new Thickness(10, 0, 0, 0);
            head.Children.Add(Row(0, _info, _count));
            SetDock(head, Dock.Top);
            Children.Add(head);
            _warning = Label(T("MODS_LOADER_WARNING", "This instance runs vanilla Minecraft. Mods need Forge, NeoForge, Fabric or Quilt (Versions tab)."), "accent", true);
            _warning.Margin = new Thickness(0, 0, 0, 8);
            SetDock(_warning, Dock.Top);
            Children.Add(_warning);
            var bar = Bar(
                Button(T("MODS_ADD_BTN", "Add mods"), AddMods, "accent", "plus"),
                Button(T("MODS_REMOVE_BTN", "Remove"), RemoveSelected, "", "trash"),
                Button(T("QT_ENABLE_DISABLE", "Enable / disable"), ToggleSelected, "", "switch"),
                Button(T("MODS_UPDATE_BTN", "Update mods"), UpdateMods, "", "update"),
                Button(T("MODS_RESTORE_BACKUPS", "Restore backups"), RestoreBackups, "", "refresh"),
                Button(T("MODS_OPEN_FOLDER_BTN", "Open folder"), () => { Directory.CreateDirectory(ModsDir()); OpenPath(ModsDir()); }, "", "folder"),
                Button(T("QT_GET_MODS", "Get mods (Modrinth)"), GoContent, "", "modrinth"));
            bar.Margin = new Thickness(0, 8, 0, 0);
            SetDock(bar, Dock.Bottom);
            Children.Add(bar);
            _selectedInfo = Label(T("MODS_SELECT_MOD_INFO", "Select a mod to see details."), "hint", true);
            _selectedInfo.Margin = new Thickness(0, 6, 0, 0);
            SetDock(_selectedInfo, Dock.Bottom);
            Children.Add(_selectedInfo);
            var view = new GridView();
            view.Columns.Add(Column(T("QT_MOD_NAME", "Mod"), "Name", 320, true));
            view.Columns.Add(Column(T("QT_VERSION", "Version"), "Version", 140, false));
            view.Columns.Add(Column(T("QT_MOD_FILE", "File"), "File", 320, false));
            _list.View = view;
            _list.Background = Brushes.Transparent;
            _list.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush");
            _list.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            _list.SelectionChanged += (_, _) => OnSelect();
            _list.MouseDoubleClick += (_, _) => ToggleSelected();
            Children.Add(_list);
        }
        private static GridViewColumn Column(string header, string path, double width, bool bold)
        {
            var f = new FrameworkElementFactory(typeof(TextBlock));
            f.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(path));
            f.SetBinding(OpacityProperty, new System.Windows.Data.Binding("Opacity"));
            f.SetBinding(ToolTipProperty, new System.Windows.Data.Binding("Tip"));
            f.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            if (bold) f.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            return new GridViewColumn { Header = header, Width = width, CellTemplate = new DataTemplate { VisualTree = f } };
        }
        public void Load(MinecraftInstance instance)
        {
            _instance = instance;
            Refresh();
        }
        private string ModsDir() => FileLinks.Resolve(_instance!.ModsDir);
        public async void Refresh()
        {
            if (_instance == null) return;
            var inst = _instance;
            var loader = char.ToUpper(inst.LoaderKey[0]) + inst.LoaderKey[1..];
            _info.Text = $"{inst.Name}  ·  {inst.Version}  ·  {loader}";
            _warning.Visibility = inst.IsVanilla ? Visibility.Visible : Visibility.Collapsed;
            var dir = ModsDir();
            var files = Directory.Exists(dir)
                ? Directory.GetFiles(dir).Where(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
            _count.Text = files.Count > 0 ? T("MODS_COUNT", "{n} mods", ("n", files.Count)) : T("MODS_NONE_INSTALLED", "No mods installed");
            var q = _search.Text.Trim();
            var rows = new List<ModRow>();
            foreach (var f in files)
            {
                var cached = ModMetadataReader.GetCached(f);
                var row = new ModRow { Path = f, Name = string.IsNullOrEmpty(cached?.Name) ? Path.GetFileName(f) : cached!.Name, Version = cached?.Version ?? "", Tip = cached?.Description };
                if (row.Disabled) row.Tip = T("QT_MOD_DISABLED_TIP", "Disabled (renamed to .jar.disabled)");
                if (q.Length > 0 && !row.File.Contains(q, StringComparison.OrdinalIgnoreCase) && !row.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                rows.Add(row);
            }
            _list.ItemsSource = rows;
            var pending = rows.Where(r => ModMetadataReader.GetCached(r.Path) == null).ToList();
            if (pending.Count == 0) return;
            var token = ++_metaToken;
            await Task.Run(() => { foreach (var r in pending) { if (token != _metaToken) return; ModMetadataReader.Read(r.Path); } });
            if (token != _metaToken) return;
            foreach (var r in pending)
            {
                var m = ModMetadataReader.GetCached(r.Path);
                if (m == null || string.IsNullOrEmpty(m.Name)) continue;
                r.Name = m.Name;
                r.Version = m.Version;
                if (!r.Disabled && !string.IsNullOrEmpty(m.Description)) r.Tip = m.Description.Length > 400 ? m.Description[..400] : m.Description;
            }
            var filtered = q.Length == 0 ? rows : rows.Where(r => r.File.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            _list.ItemsSource = filtered.ToList();
        }
        private List<string> SelectedPaths() => _list.SelectedItems.OfType<ModRow>().Select(r => r.Path).ToList();
        private void OnSelect()
        {
            var paths = SelectedPaths();
            if (paths.Count == 0) _selectedInfo.Text = T("MODS_SELECT_MOD_INFO", "Select a mod to see details.");
            else if (paths.Count == 1)
            {
                var p = paths[0];
                var meta = File.Exists(p) ? ModMetadataReader.Read(p) : new ModMetadata();
                var parts = new List<string> { string.IsNullOrEmpty(meta.Name) ? Path.GetFileName(p) : meta.Name };
                if (meta.Version.Length > 0) parts.Add("v" + meta.Version);
                if (meta.Id.Length > 0) parts.Add(meta.Id);
                if (meta.Loader.Length > 0) parts.Add(meta.Loader);
                parts.Add(Path.GetFileName(p));
                try { parts.Add(HumanSize(new FileInfo(p).Length)); } catch { }
                var text = string.Join("  ·  ", parts);
                if (meta.Description.Length > 0) text += "\n" + (meta.Description.Length > 300 ? meta.Description[..300] : meta.Description);
                _selectedInfo.Text = text;
            }
            else _selectedInfo.Text = T("QT_MODS_SELECTED", "{n} mods selected", ("n", paths.Count));
        }
        private void AddMods()
        {
            if (_instance!.IsVanilla && !Ask(T("MODS_NO_LOADER_TITLE", "No mod loader"), T("MODS_NO_LOADER_MSG", "This instance has no mod loader, mods will not load. Add them anyway?")))
                return;
            var files = PickFiles(T("MODS_FILE_SELECT_TITLE", "Select mods"), Filter((T("QT_FT_MODS", "Mod files"), "*.jar;*.zip")) + "|" + AllFiles);
            if (files == null) return;
            var dir = ModsDir();
            Directory.CreateDirectory(dir);
            int added = 0;
            foreach (var f in files)
            {
                try { File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true); added++; } catch { }
            }
            Refresh();
            _launcher.SetStatus(T("QT_MODS_ADDED", "Added {n} mod(s)", ("n", added)));
        }
        private void RemoveSelected()
        {
            var paths = SelectedPaths();
            if (paths.Count == 0)
            {
                Info(T("MODS_REMOVE_TITLE", "Remove mods"), T("MODS_REMOVE_NONE", "Select mods first."));
                return;
            }
            var msg = paths.Count == 1 ? T("MODS_REMOVE_CONFIRM_SINGLE", "Remove {name}?", ("name", Path.GetFileName(paths[0])))
                : T("MODS_REMOVE_CONFIRM_MULTI", "Remove {count} mods?", ("count", paths.Count));
            if (!Ask(T("MODS_REMOVE_CONFIRM_TITLE", "Remove"), msg)) return;
            foreach (var p in paths) { try { File.Delete(p); } catch { } }
            Refresh();
        }
        private void ToggleSelected()
        {
            foreach (var p in SelectedPaths())
            {
                try
                {
                    if (p.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) File.Move(p, p[..^".disabled".Length]);
                    else File.Move(p, p + ".disabled");
                }
                catch { }
            }
            Refresh();
        }
        private void GoContent()
        {
            InstanceManager.Instance.SetSelectedInstance(_instance!.InstanceId);
            _launcher.ShowContent("mod");
        }
        private void RestoreBackups()
        {
            var dir = ModsDir();
            var baks = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.jar.bak") : Array.Empty<string>();
            if (baks.Length == 0)
            {
                Info(T("MODS_BACKUP_NONE", "No backups"), T("MODS_BACKUP_NONE_MSG", "No .bak backup files found."));
                return;
            }
            if (!Ask(T("MODS_BACKUP_RESTORE_TITLE", "Restore backups"), T("MODS_BACKUP_RESTORE_CONFIRM", "Restore {count} backup(s)?", ("count", baks.Length)))) return;
            int n = 0;
            foreach (var bak in baks)
            {
                try { File.Move(bak, bak[..^4], true); n++; } catch { }
            }
            Info(T("MODS_BACKUP_RESTORE_DONE", "Restore complete"), T("QT_RESTORED_BACKUPS", "Restored {n} backup(s).", ("n", n)));
            Refresh();
        }
        private void UpdateMods()
        {
            var dir = ModsDir();
            if (!Directory.Exists(dir) || Directory.GetFiles(dir, "*.jar").Length == 0)
            {
                Info(T("MODS_UPDATE_TITLE", "Update mods"), T("QT_NO_MODS_TO_UPDATE", "No mods to update."));
                return;
            }
            new PackUpdateDialog(_launcher, dir, _instance!.LoaderKey, _instance.Version, [".jar"], T("MODS_UPDATE_TITLE", "Update mods"), Refresh).Start();
        }
    }
}
