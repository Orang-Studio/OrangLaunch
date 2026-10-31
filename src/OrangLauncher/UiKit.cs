using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OrangLauncher.Managers;
namespace OrangLauncher
{
    public static class UiKit
    {
        public static Window? Owner { get; set; }
        private static readonly ConcurrentDictionary<string, ImageSource?> IconCache = new();
        public static string T(string key, string def, params (string, object?)[] args) => LocalizationManager.T(key, def, args);
        public static string AppDir => AppDomain.CurrentDomain.BaseDirectory;
        public static string? FindAsset(string relative)
        {
            foreach (var root in new[] { System.IO.Path.Combine(AppDir, "Other"), System.IO.Path.Combine(AppDir, "..", "..", "..", "..", "..", "..", "assets", "Other") })
            {
                var p = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
                if (File.Exists(p)) return p;
            }
            return null;
        }
        public static ImageSource? IconSource(string name)
        {
            return IconCache.GetOrAdd(name, n =>
            {
                var file = n switch
                {
                    "plus" => "add",
                    "trash" => "delete",
                    "file" => "install",
                    "switch" => "select",
                    "user" => "accounts",
                    "modrinth" => "search",
                    _ => n
                };
                var path = FindAsset($"images/icons/{file}.png");
                return path == null ? null : LoadImage(path);
            });
        }
        public static FrameworkElement Icon(string name, double size = 16, Brush? fill = null)
        {
            var rect = new Rectangle { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, SnapsToDevicePixels = true };
            var src = IconSource(name);
            if (src != null) rect.OpacityMask = new ImageBrush(src) { Stretch = Stretch.Uniform };
            if (fill != null) rect.Fill = fill;
            else rect.SetBinding(Shape.FillProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1), FallbackValue = Brushes.Gray });
            return rect;
        }
        public static BitmapImage? LoadImage(string? path, int decodeWidth = 0)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                fs.CopyTo(ms);
                return ImageFromBytes(ms.ToArray(), decodeWidth);
            }
            catch { return null; }
        }
        public static byte[]? ToPng(byte[] data)
        {
            try
            {
                using var bmp = SkiaSharp.SKBitmap.Decode(data);
                if (bmp == null) return null;
                using var img = SkiaSharp.SKImage.FromBitmap(bmp);
                using var enc = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                return enc.ToArray();
            }
            catch { return null; }
        }
        public static BitmapImage? ImageFromBytes(byte[]? data, int decodeWidth = 0)
        {
            if (data == null || data.Length == 0) return null;
            var isWebp = data.Length > 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P';
            if (isWebp) data = ToPng(data);
            if (data == null) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
                bmp.StreamSource = new MemoryStream(data);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
        public static Brush Res(string key) => Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
        public static void Dyn(FrameworkElement e, DependencyProperty p, string key) => e.SetResourceReference(p, key);
        public static TextBlock Label(string text, string kind = "", bool wrap = false)
        {
            var tb = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
            if (wrap) tb.TextWrapping = TextWrapping.Wrap;
            switch (kind)
            {
                case "h1": tb.FontSize = 20; tb.FontWeight = FontWeights.Bold; Dyn(tb, TextBlock.ForegroundProperty, "FgPrimaryBrush"); break;
                case "h2": tb.FontSize = 16; tb.FontWeight = FontWeights.Bold; Dyn(tb, TextBlock.ForegroundProperty, "FgPrimaryBrush"); break;
                case "h3": tb.FontSize = 14; tb.FontWeight = FontWeights.Bold; Dyn(tb, TextBlock.ForegroundProperty, "FgPrimaryBrush"); break;
                case "muted": tb.FontSize = 12; Dyn(tb, TextBlock.ForegroundProperty, "FgSecondaryBrush"); break;
                case "hint": tb.FontSize = 11.5; Dyn(tb, TextBlock.ForegroundProperty, "FgTertiaryBrush"); tb.Opacity = 0.9; break;
                case "accent": Dyn(tb, TextBlock.ForegroundProperty, "AccentPrimaryBrush"); break;
                default: Dyn(tb, TextBlock.ForegroundProperty, "FgPrimaryBrush"); break;
            }
            return tb;
        }
        public static Button Button(string text, Action? onClick = null, string kind = "", string? icon = null, string? tooltip = null)
        {
            var b = new Button();
            b.VerticalAlignment = VerticalAlignment.Center;
            b.SetResourceReference(FrameworkElement.StyleProperty, kind switch
            {
                "accent" => "OlAccentButton",
                "danger" => "OlDangerButton",
                "flat" => "OlFlatButton",
                _ => "OlButton"
            });
            SetButtonContent(b, text, icon);
            if (tooltip != null) b.ToolTip = tooltip;
            if (onClick != null) b.Click += (_, _) => onClick();
            return b;
        }
        public static void SetButtonContent(Button b, string text, string? icon)
        {
            if (icon == null)
            {
                b.Content = text;
                return;
            }
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(Icon(icon, 15));
            sp.Children.Add(new TextBlock { Text = text, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            b.Content = sp;
        }
        public static StackPanel Row(double spacing, params UIElement[] children)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var c in children) AddSpaced(sp, c, spacing);
            return sp;
        }
        public static void AddSpaced(Panel panel, UIElement child, double spacing = 8)
        {
            if (child is FrameworkElement fe && panel.Children.Count > 0)
            {
                var m = fe.Margin;
                if (panel is StackPanel { Orientation: Orientation.Horizontal } || panel is WrapPanel)
                    fe.Margin = new Thickness(m.Left + spacing, m.Top, m.Right, m.Bottom);
                else
                    fe.Margin = new Thickness(m.Left, m.Top + spacing, m.Right, m.Bottom);
            }
            panel.Children.Add(child);
        }
        public static WrapPanel Bar(params UIElement[] children)
        {
            var wp = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var c in children)
            {
                if (c is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 8, 6);
                wp.Children.Add(c);
            }
            return wp;
        }
        public static Border Card(string? title, out StackPanel body)
        {
            body = new StackPanel();
            var border = new Border { Child = body, Margin = new Thickness(0, 0, 0, 14) };
            border.SetResourceReference(FrameworkElement.StyleProperty, "OlCard");
            if (!string.IsNullOrEmpty(title))
            {
                var head = Label(title, "h3");
                head.Margin = new Thickness(0, 0, 0, 10);
                body.Children.Add(head);
            }
            return border;
        }
        public static void Add(StackPanel body, UIElement child, double top = 8)
        {
            if (child is FrameworkElement fe && body.Children.Count > 0)
                fe.Margin = new Thickness(fe.Margin.Left, fe.Margin.Top + top, fe.Margin.Right, fe.Margin.Bottom);
            body.Children.Add(child);
        }
        public static FrameworkElement ToggleRow(string title, string description, bool value, Action<bool>? onChange, out CheckBox toggle)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var text = new StackPanel();
            text.Children.Add(Label(title, "h3", true));
            if (!string.IsNullOrEmpty(description))
            {
                var d = Label(description, "muted", true);
                d.Margin = new Thickness(0, 2, 16, 0);
                text.Children.Add(d);
            }
            grid.Children.Add(text);
            var cb = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Center };
            cb.SetResourceReference(FrameworkElement.StyleProperty, "OlSwitch");
            Grid.SetColumn(cb, 1);
            grid.Children.Add(cb);
            if (onChange != null)
            {
                cb.Checked += (_, _) => onChange(true);
                cb.Unchecked += (_, _) => onChange(false);
            }
            text.MouseLeftButtonUp += (_, _) => cb.IsChecked = cb.IsChecked != true;
            toggle = cb;
            return grid;
        }
        public static FrameworkElement ToggleRow(string title, string description, bool value, Action<bool>? onChange)
            => ToggleRow(title, description, value, onChange, out _);
        public static FrameworkElement FormRow(string label, UIElement control, string? hint = null, double labelWidth = 0)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            var l = Label(label, "h3");
            l.FontWeight = FontWeights.Normal;
            l.Margin = new Thickness(0, 0, 0, 4);
            sp.Children.Add(l);
            sp.Children.Add(control);
            if (!string.IsNullOrEmpty(hint))
            {
                var h = Label(hint, "hint", true);
                h.Margin = new Thickness(0, 3, 0, 0);
                sp.Children.Add(h);
            }
            return sp;
        }
        public static Grid Form(double labelWidth = 170)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return g;
        }
        public static void FormAdd(Grid g, string label, UIElement control)
        {
            var row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = Label(label, "muted");
            l.HorizontalAlignment = HorizontalAlignment.Right;
            l.Margin = new Thickness(0, 6, 12, 6);
            l.TextAlignment = TextAlignment.Right;
            l.TextWrapping = TextWrapping.Wrap;
            Grid.SetRow(l, row);
            g.Children.Add(l);
            if (control is FrameworkElement fe) fe.Margin = new Thickness(0, 4, 0, 4);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            g.Children.Add(control);
        }
        public static ScrollViewer Scroll(UIElement content, bool horizontal = false)
            => new()
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                Focusable = false
            };
        public static (ScrollViewer Scroll, StackPanel Body) Page(Thickness? margin = null, double maxWidth = 980)
        {
            var body = new StackPanel { Margin = margin ?? new Thickness(24, 18, 24, 18) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = maxWidth });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Auto) });
            grid.Children.Add(body);
            return (Scroll(grid), body);
        }
        public static TextBox TextBox(string text = "", string? placeholder = null, bool multiline = false, double height = 0)
        {
            var tb = new TextBox { Text = text, Tag = placeholder ?? "" };
            if (multiline)
            {
                tb.AcceptsReturn = true;
                tb.TextWrapping = TextWrapping.Wrap;
                tb.VerticalContentAlignment = VerticalAlignment.Top;
                tb.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                tb.Height = height > 0 ? height : 70;
            }
            return tb;
        }
        public static ComboBox Combo(double width = 0)
        {
            var cb = new ComboBox();
            if (width > 0) cb.Width = width;
            return cb;
        }
        public static ComboBoxItem Item(string text, object? tag) => new() { Content = text, Tag = tag };
        public static object? SelectedTag(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag;
        public static void SelectTag(ComboBox cb, object? tag)
        {
            for (int i = 0; i < cb.Items.Count; i++)
                if (cb.Items[i] is ComboBoxItem it && Equals(it.Tag, tag)) { cb.SelectedIndex = i; return; }
        }
        public static string HumanSize(long n)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double v = n;
            int i = 0;
            while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
            return i == 0 ? $"{n} B" : $"{v:0.#} {units[i]}";
        }
        public static void OpenPath(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false });
            }
            catch (Exception ex) { Error(T("ERROR", "Error"), ex.Message); }
        }
        private static void RunOverride(string command, string target)
        {
            var parts = LaunchService.SplitArgs(command);
            var psi = new ProcessStartInfo(parts[0]) { UseShellExecute = false };
            foreach (var a in parts.Skip(1)) psi.ArgumentList.Add(a);
            psi.ArgumentList.Add(target);
            Process.Start(psi);
        }
        public static void OpenUrl(string url)
        {
            try
            {
                var custom = LauncherSettings.CustomBrowser;
                if (!string.IsNullOrWhiteSpace(custom)) RunOverride(custom, url);
                else Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) { Error(T("ERROR", "Error"), ex.Message); }
        }
        public static void OpenInEditor(string path)
        {
            try
            {
                var custom = LauncherSettings.CustomEditor;
                if (!string.IsNullOrWhiteSpace(custom)) RunOverride(custom, path);
                else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                try { Process.Start(new ProcessStartInfo("notepad.exe") { ArgumentList = { path } }); } catch { }
            }
        }
        public static string? PickFile(string title, string filter)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = title, Filter = filter };
            return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
        }
        public static string[]? PickFiles(string title, string filter)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = title, Filter = filter, Multiselect = true };
            return dlg.ShowDialog(Owner) == true ? dlg.FileNames : null;
        }
        public static string? SaveFile(string title, string filter, string fileName)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Title = title, Filter = filter, FileName = fileName };
            return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
        }
        public static string Filter(params (string Name, string Pattern)[] parts)
            => string.Join("|", parts.Select(p => $"{p.Name} ({p.Pattern.Replace(";", ", ")})|{p.Pattern}"));
        public static string AllFiles => $"{T("QT_FT_ALL", "All files")} (*.*)|*.*";
        private static Window? DialogOwner() => Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Owner;
        private static MessageBoxResult Box(string title, string message, MessageBoxButton buttons, MessageBoxImage image)
        {
            var owner = DialogOwner();
            return OlMessageBox.Show(owner != null && owner.IsLoaded ? owner : null, message, title, buttons, image);
        }
        public static void Info(string title, string message) => Box(title, message, MessageBoxButton.OK, MessageBoxImage.Information);
        public static void Warn(string title, string message) => Box(title, message, MessageBoxButton.OK, MessageBoxImage.Warning);
        public static void Error(string title, string message) => Box(title, message, MessageBoxButton.OK, MessageBoxImage.Error);
        public static bool Ask(string title, string message) => Box(title, message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        public static string? AskString(string title, string prompt, string initial = "")
        {
            var dlg = new AddTextDialog(prompt, title, initial);
            var owner = DialogOwner();
            if (owner != null && owner.IsLoaded) dlg.Owner = owner;
            else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return dlg.ShowDialog() == true ? dlg.ResultText : null;
        }
        private static readonly (Type Type, string Key)[] OriginalStyles =
        [
            (typeof(Button), "DefaultButtonStyle"),
            (typeof(TextBox), "DefaultTextBoxStyle"),
            (typeof(ComboBox), "DefaultComboBoxStyle"),
            (typeof(ListBox), "DefaultListBoxStyle"),
            (typeof(TabControl), "DefaultTabControlStyle"),
            (typeof(TabItem), "DefaultTabItemStyle")
        ];
        private static void ApplyOriginalStyles(FrameworkElement root)
        {
            foreach (var (type, key) in OriginalStyles)
            {
                if (Application.Current?.TryFindResource(key) is Style s) root.Resources[type] = new Style(type, s);
                else root.Resources.Remove(type);
            }
            if (!root.Resources.Contains(typeof(RadioButton)))
            {
                var radio = new Style(typeof(RadioButton));
                radio.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("FgPrimaryBrush")));
                root.Resources[typeof(RadioButton)] = radio;
            }
        }
        public static void UseOriginalStyles(FrameworkElement root)
        {
            ApplyOriginalStyles(root);
            Action<string> handler = _ => ApplyOriginalStyles(root);
            ThemeManager.ThemeChanged += handler;
            if (root is Window w) w.Closed += (_, _) => ThemeManager.ThemeChanged -= handler;
        }
        public static void Copy(string text)
        {
            try { Clipboard.SetText(text); } catch { }
        }
        public static void OnUi(Action a)
        {
            var d = Application.Current?.Dispatcher;
            if (d == null) return;
            if (d.CheckAccess()) a();
            else d.BeginInvoke(a);
}   }   }