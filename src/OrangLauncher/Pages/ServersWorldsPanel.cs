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
    public class ServersWorldsPanel : DockPanel
    {
        private readonly MainWindow _launcher;
        private MinecraftInstance? _instance;
        private List<ServerInfo> _servers = new();
        private readonly TextBlock _info = Label("", "muted");
        private readonly ListView _table = new() { SelectionMode = SelectionMode.Single };
        private readonly Button _join, _edit, _rename, _folder, _delete;
        private int _pingToken;
        private sealed class Row : System.ComponentModel.INotifyPropertyChanged
        {
            public string Kind { get; init; } = "";
            public int Index { get; init; }
            public WorldInfo? World { get; init; }
            public ImageSource? Icon { get; set; }
            public string Name { get; init; } = "";
            public string Second { get; init; } = "";
            public string Third { get; set; } = "";
            public string Fourth { get; init; } = "";
            public Brush? ThirdBrush { get; set; }
            public string? Tip { get; set; }
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            public void Changed() { foreach (var p in new[] { nameof(Icon), nameof(Third), nameof(ThirdBrush), nameof(Tip) }) PropertyChanged?.Invoke(this, new(p)); }
        }
        public ServersWorldsPanel(MainWindow launcher)
        {
            _launcher = launcher;
            Margin = new Thickness(14, 12, 14, 12);
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            _join = Button(T("QT_JOIN", "Join"), QuickPlay, "accent");
            _edit = Button(T("QT_EDIT", "Edit"), EditServer, "", "settings");
            _rename = Button(T("QT_RENAME", "Rename"), RenameWorld, "", "edit");
            _folder = Button(T("QT_FOLDER", "Folder"), OpenFolder, "", "folder");
            _delete = Button(T("QT_DELETE", "Delete"), DeleteSelected, "", "trash");
            var buttons = UiKit.Row(6, Button(T("QT_REFRESH", "Refresh"), Refresh, "", "refresh"), Button(T("SERVERS_ADD", "Add server"), () => ServerDialog(null), "", "plus"), _join, _edit, _rename, _folder, _delete);
            DockPanel.SetDock(buttons, Dock.Right);
            head.Children.Add(buttons);
            _info.Margin = new Thickness(10, 0, 0, 0);
            head.Children.Add(UiKit.Row(0, Label(T("QT_TAB_SERVERS_WORLDS", "Servers and Worlds"), "h2"), _info));
            SetDock(head, Dock.Top);
            Children.Add(head);
            var view = new GridView();
            var iconF = new FrameworkElementFactory(typeof(Image));
            iconF.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding("Icon"));
            iconF.SetValue(WidthProperty, 32.0);
            iconF.SetValue(HeightProperty, 32.0);
            view.Columns.Add(new GridViewColumn { Header = "", Width = 48, CellTemplate = new DataTemplate { VisualTree = iconF } });
            view.Columns.Add(Text(T("QT_NAME", "Name"), "Name", 260, true));
            view.Columns.Add(Text(T("QT_ADDRESS", "Address") + " / " + T("QT_FOLDER", "Folder"), "Second", 240, false));
            view.Columns.Add(Text(T("QT_STATUS", "Status") + " / " + T("QT_SIZE", "Size"), "Third", 190, false, "ThirdBrush"));
            view.Columns.Add(Text(T("QT_LAST_PLAYED", "Last played"), "Fourth", 150, false));
            _table.View = view;
            _table.Background = Brushes.Transparent;
            _table.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush");
            _table.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            _table.SelectionChanged += (_, _) => OnSelect();
            _table.MouseDoubleClick += (_, _) => Activate();
            Children.Add(_table);
            OnSelect();
        }
        private static GridViewColumn Text(string header, string path, double width, bool bold, string? brushPath = null)
        {
            var f = new FrameworkElementFactory(typeof(TextBlock));
            f.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(path));
            f.SetBinding(ToolTipProperty, new System.Windows.Data.Binding("Tip"));
            f.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            f.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            if (bold) f.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            if (brushPath != null) f.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(brushPath) { TargetNullValue = DependencyProperty.UnsetValue });
            return new GridViewColumn { Header = header, Width = width, CellTemplate = new DataTemplate { VisualTree = f } };
        }
        public void Load(MinecraftInstance instance)
        {
            _instance = instance;
            Refresh();
        }
        private Row? Selected => _table.SelectedItem as Row;
        private void OnSelect()
        {
            var sel = Selected;
            _join.IsEnabled = _edit.IsEnabled = sel?.Kind == "server";
            _rename.IsEnabled = _folder.IsEnabled = sel?.Kind == "world";
            _delete.IsEnabled = sel != null;
        }
        private void Activate()
        {
            var sel = Selected;
            if (sel == null) return;
            if (sel.Kind == "server") QuickPlay();
            else if (sel.World != null) OpenPath(sel.World.Path);
        }
        public void Refresh()
        {
            if (_instance == null) return;
            _servers = ServerListManager.Instance.LoadServers(_instance.MinecraftDir);
            var worlds = WorldManager.List(_instance);
            var serverIcon = LoadImage(FindAsset("images/icons/server.png"), 64);
            var worldIcon = LoadImage(FindAsset("images/minecraft-green.png"), 64);
            var rows = new List<Row>();
            for (int i = 0; i < _servers.Count; i++)
            {
                var s = _servers[i];
                ImageSource? icon = null;
                if (!string.IsNullOrEmpty(s.Icon))
                {
                    try { icon = ImageFromBytes(Convert.FromBase64String(s.Icon.Split(',').Last()), 64); } catch { }
                }
                rows.Add(new Row { Kind = "server", Index = i, Icon = icon ?? serverIcon, Name = s.Name, Second = s.Ip, Third = "⏳" });
            }
            foreach (var w in worlds)
            {
                var size = w.Size < 1024L * 1024 * 1024 ? $"{w.Size / (1024.0 * 1024):0.0} MB" : $"{w.Size / (1024.0 * 1024 * 1024):0.00} GB";
                rows.Add(new Row { Kind = "world", World = w, Icon = worldIcon, Name = w.Name, Second = w.Folder, Third = size, Fourth = w.Modified.ToString("yyyy-MM-dd HH:mm") });
            }
            _table.ItemsSource = rows;
            _info.Text = T("QT_SERVERS_COUNT", "{n} server(s)", ("n", _servers.Count)) + "  ·  " + T("QT_WORLDS_COUNT", "{n} worlds", ("n", worlds.Count));
            OnSelect();
            PingAll(rows);
        }
        private void PingAll(List<Row> rows)
        {
            var token = ++_pingToken;
            foreach (var row in rows.Where(r => r.Kind == "server"))
            {
                var r = row;
                _ = Task.Run(async () =>
                {
                    var result = await ServerPinger.PingAsync(r.Second);
                    OnUi(() =>
                    {
                        if (token != _pingToken) return;
                        if (result != null)
                        {
                            var ms = result.LatencyMs;
                            r.Third = T("QT_ONLINE", "{online}/{max} online", ("online", result.Online), ("max", result.Max)) + $"  ·  {ms} ms";
                            r.ThirdBrush = new SolidColorBrush(ms < 80 ? Color.FromRgb(0x3B, 0xC6, 0x52) : ms < 200 ? Color.FromRgb(0xFF, 0xC1, 0x07) : Color.FromRgb(0xF2, 0x5C, 0x5C));
                            if (!string.IsNullOrEmpty(result.Motd)) r.Tip = result.Motd;
                            if (!string.IsNullOrEmpty(result.Favicon))
                            {
                                try { r.Icon = ImageFromBytes(Convert.FromBase64String(result.Favicon.Split(',').Last()), 64) ?? r.Icon; } catch { }
                            }
                        }
                        else
                        {
                            r.Third = T("QT_OFFLINE", "Offline");
                            r.ThirdBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0x5C, 0x5C));
                        }
                        r.Changed();
                    });
                });
            }
        }
        private void QuickPlay()
        {
            var sel = Selected;
            if (sel?.Kind != "server" || _instance == null) return;
            if (McVersion.Below(_instance.Version, 1, 20, 1) && !McVersion.Below(_instance.Version, 1, 3))
            {
                if (!Ask(T("SERVERS_QUICKPLAY", "Quick Play"), T("QT_QUICKPLAY_LEGACY", "Quick Play needs Minecraft 1.20.1 or newer. On this version the launcher passes --server instead, which older clients also understand. Continue?")))
                    return;
            }
            var ip = _servers[sel.Index].Ip.Trim();
            if (ip.Length == 0) return;
            InstanceManager.Instance.SetSelectedInstance(_instance.InstanceId);
            _launcher.PendingQuickPlay = ip;
            _launcher.LaunchGame();
        }
        private void ServerDialog(int? editIndex)
        {
            var srv = editIndex.HasValue ? _servers[editIndex.Value] : new ServerInfo();
            var win = new OlWindow(editIndex.HasValue ? T("SERVERS_EDIT_TITLE", "Edit server") : T("SERVERS_ADD_TITLE", "Add server"), 440, 260) { SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
            var body = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            var name = TextBox(srv.Name);
            var ip = TextBox(srv.Ip);
            body.Children.Add(FormRow(T("SERVERS_NAME_LABEL", "Name"), name));
            body.Children.Add(FormRow(T("SERVERS_ADDRESS_LABEL", "Address"), ip));
            bool ok = false;
            var save = Button(T("QT_SAVE", "Save"), () => { ok = true; win.Close(); }, "accent");
            save.IsDefault = true;
            var bar = UiKit.Row(8, Button(T("CANCEL", "Cancel"), win.Close), save);
            bar.HorizontalAlignment = HorizontalAlignment.Right;
            bar.Margin = new Thickness(0, 12, 0, 0);
            body.Children.Add(bar);
            win.Content = body;
            win.ShowDialog();
            if (!ok) return;
            var n = name.Text.Trim();
            var a = ip.Text.Trim();
            if (n.Length == 0 || a.Length == 0)
            {
                Warn(T("SERVERS_INVALID_TITLE", "Invalid"), T("SERVERS_INVALID_MSG", "Name and address are required."));
                return;
            }
            if (editIndex.HasValue)
            {
                srv.Name = n;
                srv.Ip = a;
            }
            else _servers.Add(new ServerInfo { Name = n, Ip = a });
            SaveServers();
        }
        private void EditServer()
        {
            if (Selected?.Kind == "server") ServerDialog(Selected.Index);
        }
        private void SaveServers()
        {
            try { ServerListManager.Instance.SaveServers(_instance!.MinecraftDir, _servers); }
            catch (Exception ex) { Error(T("ERROR", "Error"), ex.Message); }
            Refresh();
        }
        private void OpenFolder()
        {
            var sel = Selected;
            if (sel?.World != null) OpenPath(sel.World.Path);
            else if (_instance != null) { Directory.CreateDirectory(_instance.SavesDir); OpenPath(_instance.SavesDir); }
        }
        private void RenameWorld()
        {
            var sel = Selected;
            if (sel?.World == null)
            {
                Info(T("QT_WORLDS", "Worlds"), T("QT_SELECT_WORLD", "Select a world first."));
                return;
            }
            var w = sel.World;
            var newName = AskString(T("QT_RENAME", "Rename"), T("QT_WORLD_NAME", "World name") + ":", w.Name)?.Trim();
            if (string.IsNullOrEmpty(newName) || newName == w.Name) return;
            if (!WorldManager.SetLevelName(Path.Combine(w.Path, "level.dat"), newName))
            {
                Error(T("QT_RENAME", "Rename"), T("QT_LEVEL_DAT_FAIL", "Could not update level.dat."));
                return;
            }
            var safe = Regex.Replace(newName, @"[<>:""/\\|?*]", "_").Trim();
            if (safe.Length == 0) safe = w.Folder;
            var target = Path.Combine(Path.GetDirectoryName(w.Path)!, safe);
            if (!string.Equals(target, w.Path, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(target))
            {
                try { Directory.Move(w.Path, target); } catch { }
            }
            Refresh();
        }
        private void DeleteSelected()
        {
            var sel = Selected;
            if (sel == null) return;
            if (sel.Kind == "server")
            {
                var srv = _servers[sel.Index];
                if (Ask(T("SERVERS_DELETE_TITLE", "Delete server"), T("SERVERS_DELETE_CONFIRM", "Delete {name}?", ("name", srv.Name))))
                {
                    _servers.RemoveAt(sel.Index);
                    SaveServers();
                }
                return;
            }
            var w = sel.World!;
            if (!Ask(T("QT_DELETE", "Delete"), T("QT_DELETE_WORLD_CONFIRM", "Permanently delete the world '{name}'?\n\nFolder: {path}\nThis cannot be undone.", ("name", w.Name), ("path", w.Path)))) return;
            try { Directory.Delete(w.Path, true); }
            catch (Exception ex) { Error(T("QT_DELETE", "Delete"), ex.Message); }
            Refresh();
}   }   }