using System.IO;
using System.Windows;
using System.Windows.Controls;
using OrangLauncher.Managers;
using OrangLauncher.Models;
using static OrangLauncher.UiKit;
namespace OrangLauncher.Pages
{
    public class PacksPanel : Grid
    {
        private readonly MainWindow _launcher;
        private MinecraftInstance? _instance;
        private sealed class Column
        {
            public string Kind = "";
            public ListBox List = null!;
            public TextBlock Count = null!;
            public TextBlock Info = null!;
            public TextBlock Shared = null!;
        }
        private readonly Column _rp, _sp;
        public PacksPanel(MainWindow launcher)
        {
            _launcher = launcher;
            Margin = new Thickness(14, 12, 14, 12);
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _rp = Build("resourcepacks", T("RES_SH_RP_TITLE", "Resource packs"), out var rpBox);
            _sp = Build("shaderpacks", T("RES_SH_SP_TITLE", "Shader packs"), out var spBox);
            Children.Add(rpBox);
            SetColumn(spBox, 2);
            Children.Add(spBox);
        }
        private Column Build(string kind, string title, out DockPanel box)
        {
            var col = new Column { Kind = kind };
            box = new DockPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            col.Count = Label("", "muted");
            col.Count.Margin = new Thickness(10, 0, 0, 0);
            head.Children.Add(Row(0, Label(title, "h2"), col.Count));
            DockPanel.SetDock(head, Dock.Top);
            box.Children.Add(head);
            col.Shared = Label(T("QT_PACKS_SHARED", "Synced with all instances (Settings > General > Shared files)"), "accent", true);
            col.Shared.Margin = new Thickness(0, 0, 0, 6);
            col.Shared.Visibility = Visibility.Collapsed;
            DockPanel.SetDock(col.Shared, Dock.Top);
            box.Children.Add(col.Shared);
            var b2 = Bar(
                Button(T("QT_UPDATE_MODRINTH", "Update (Modrinth)"), () => Update(kind), "", "update"),
                Button(T("QT_BROWSE_MODRINTH", "Browse Modrinth"), () => Browse(kind), "", "modrinth"));
            DockPanel.SetDock(b2, Dock.Bottom);
            box.Children.Add(b2);
            var b1 = Bar(
                Button(kind == "resourcepacks" ? T("RES_SH_RP_ADD", T("QT_ADD", "Add")) : T("RES_SH_SP_ADD", T("QT_ADD", "Add")), () => Add(kind), "accent", "plus"),
                Button(T("RES_SH_REMOVE_SELECTED", "Remove"), () => Remove(kind), "", "trash"),
                Button(T("RES_SH_OPEN_FOLDER", "Open folder"), () => { Directory.CreateDirectory(Dir(kind)); OpenPath(Dir(kind)); }, "", "folder"));
            b1.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(b1, Dock.Bottom);
            box.Children.Add(b1);
            col.Info = Label("", "hint");
            col.Info.Margin = new Thickness(0, 4, 0, 0);
            DockPanel.SetDock(col.Info, Dock.Bottom);
            box.Children.Add(col.Info);
            col.List = new ListBox { SelectionMode = SelectionMode.Extended };
            col.List.SelectionChanged += (_, _) => OnSelect(col);
            col.List.AllowDrop = true;
            col.List.Drop += (_, e) =>
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files) CopyIn(kind, files);
            };
            box.Children.Add(col.List);
            return col;
        }
        private string Dir(string kind) => FileLinks.Resolve(kind == "resourcepacks" ? _instance!.ResourcePacksDir : _instance!.ShaderPacksDir);
        public void Load(MinecraftInstance instance)
        {
            _instance = instance;
            Refresh();
        }
        public void Refresh()
        {
            if (_instance == null) return;
            foreach (var col in new[] { _rp, _sp })
            {
                col.List.Items.Clear();
                var link = col.Kind == "resourcepacks" ? _instance.ResourcePacksDir : _instance.ShaderPacksDir;
                col.Shared.Visibility = FileLinks.IsLink(link) ? Visibility.Visible : Visibility.Collapsed;
                var d = Dir(col.Kind);
                var names = new List<string>();
                if (Directory.Exists(d))
                {
                    foreach (var e in new DirectoryInfo(d).GetFileSystemInfos().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        if (e is FileInfo f && f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)) names.Add(f.Name);
                        else if (e is DirectoryInfo di && !di.Name.StartsWith(".")) names.Add(di.Name);
                    }
                }
                foreach (var n in names) col.List.Items.Add(n);
                col.Count.Text = T("QT_PACKS_COUNT", "{n} pack(s)", ("n", names.Count));
                col.Info.Text = "";
            }
        }
        private void OnSelect(Column col)
        {
            var items = col.List.SelectedItems.OfType<string>().ToList();
            if (items.Count == 1)
            {
                var p = Path.Combine(Dir(col.Kind), items[0]);
                try
                {
                    long size = File.Exists(p) ? new FileInfo(p).Length : new DirectoryInfo(p).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                    col.Info.Text = $"{items[0]}  ·  {HumanSize(size)}";
                }
                catch { col.Info.Text = items[0]; }
            }
            else col.Info.Text = items.Count > 0 ? T("QT_N_SELECTED", "{n} selected", ("n", items.Count)) : "";
        }
        private void CopyIn(string kind, IEnumerable<string> files)
        {
            var d = Dir(kind);
            Directory.CreateDirectory(d);
            foreach (var f in files)
            {
                try { File.Copy(f, Path.Combine(d, Path.GetFileName(f)), true); } catch { }
            }
            Refresh();
        }
        private void Add(string kind)
        {
            var files = PickFiles(T("QT_SELECT_PACKS", "Select packs"), Filter((T("QT_FT_PACKS", "Packs"), "*.zip")) + "|" + AllFiles);
            if (files != null) CopyIn(kind, files);
        }
        private void Remove(string kind)
        {
            var col = kind == "resourcepacks" ? _rp : _sp;
            var names = col.List.SelectedItems.OfType<string>().ToList();
            if (names.Count == 0)
            {
                Info(T("QT_PACKS", "Packs"), T("QT_SELECT_PACKS_FIRST", "Select packs first."));
                return;
            }
            if (!Ask(T("CONFIRM_REMOVE", "Remove"), T("QT_REMOVE_PACKS_CONFIRM", "Remove {n} pack(s)?", ("n", names.Count)))) return;
            var d = Dir(kind);
            foreach (var n in names)
            {
                var p = Path.Combine(d, n);
                try
                {
                    if (Directory.Exists(p)) Directory.Delete(p, true);
                    else File.Delete(p);
                }
                catch { }
            }
            Refresh();
        }
        private void Update(string kind)
        {
            var inst = _instance!;
            var d = Dir(kind);
            if (!Directory.Exists(d))
            {
                Info(T("QT_UPDATE", "Update"), T("QT_FOLDER_MISSING", "Folder does not exist yet."));
                return;
            }
            var loader = kind == "resourcepacks" ? "minecraft" : (inst.LoaderKey is "fabric" or "quilt" or "neoforge" or "forge" ? "iris" : "optifine");
            var title = kind == "resourcepacks" ? T("RES_SH_RP_TITLE", "Resource packs") : T("RES_SH_SP_TITLE", "Shader packs");
            new PackUpdateDialog(_launcher, d, loader, inst.Version, [".zip"], T("QT_UPDATE", "Update") + ": " + title, Refresh).Start();
        }
        private void Browse(string kind)
        {
            InstanceManager.Instance.SetSelectedInstance(_instance!.InstanceId);
            _launcher.ShowContent(kind == "resourcepacks" ? "resourcepack" : "shader");
        }
    }
}
