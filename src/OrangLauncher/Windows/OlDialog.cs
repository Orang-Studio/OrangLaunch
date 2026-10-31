using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static OrangLauncher.UiKit;
namespace OrangLauncher
{
    public class OlWindow : Window
    {
        public OlWindow(string title, double width, double height)
        {
            Title = title;
            Width = width;
            Height = height;
            MinWidth = Math.Min(width, 360);
            MinHeight = Math.Min(height, 200);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            UseLayoutRounding = true;
            SetResourceReference(BackgroundProperty, "BgPrimaryBrush");
            SetResourceReference(ForegroundProperty, "FgPrimaryBrush");
            UseOriginalStyles(this);
            OlChrome.Apply(this);
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? UiKit.Owner;
            if (owner != null && owner != this && owner.IsLoaded) Owner = owner;
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var icon = FindAsset("images/orange.ico");
            if (icon != null) Icon = LoadImage(icon);
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
            Loaded += (_, _) =>
            {
                try
                {
                    Managers.WindowStyleManager.ApplyTitleBar(this);
                }
                catch { }
            };
        }
    }
    public static class OlMessageBox
    {
        public static MessageBoxResult Show(string text) => Show(null, text, "OrangLauncher", MessageBoxButton.OK, MessageBoxImage.None);
        public static MessageBoxResult Show(string text, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
            => Show(null, text, caption, button, image);
        public static MessageBoxResult Show(Window? owner, string text, string caption, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            var kind = image switch
            {
                MessageBoxImage.Error => "error",
                MessageBoxImage.Warning => "warn",
                MessageBoxImage.Question => "question",
                _ => "info"
            };
            var (labels, results) = button switch
            {
                MessageBoxButton.YesNo => (new[] { T("QT_YES", "Yes"), T("QT_NO", "No") }, new[] { MessageBoxResult.Yes, MessageBoxResult.No }),
                MessageBoxButton.YesNoCancel => (new[] { T("QT_YES", "Yes"), T("QT_NO", "No"), T("CANCEL", "Cancel") }, new[] { MessageBoxResult.Yes, MessageBoxResult.No, MessageBoxResult.Cancel }),
                MessageBoxButton.OKCancel => (new[] { T("QT_OK", "OK"), T("CANCEL", "Cancel") }, new[] { MessageBoxResult.OK, MessageBoxResult.Cancel }),
                _ => (new[] { T("QT_OK", "OK") }, new[] { MessageBoxResult.OK })
            };
            var index = OlDialog.Show(caption, text, kind, owner, labels);
            if (index < 0) return button == MessageBoxButton.OK ? MessageBoxResult.OK : button == MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.Cancel;
            return results[index];
        }
    }
    public static class OlDialog
    {
        public static int Show(string title, string message, string kind, params string[] buttons) => Show(title, message, kind, null, buttons);
        public static int Show(string title, string message, string kind, Window? owner, params string[] buttons)
        {
            var win = new OlWindow(title, 480, 220) { SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
            if (owner != null && owner != win && owner.IsLoaded) { win.Owner = owner; win.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            int result = -1;
            var root = new DockPanel { Margin = new Thickness(22, 18, 22, 16) };
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
            DockPanel.SetDock(bar, Dock.Bottom);
            for (int i = 0; i < buttons.Length; i++)
            {
                var idx = i;
                var b = Button(buttons[i], () => { result = idx; win.Close(); }, i == 0 ? (kind == "error" ? "" : "accent") : "");
                b.MinWidth = 90;
                b.Margin = new Thickness(8, 0, 0, 0);
                if (i == 0) b.IsDefault = true;
                bar.Children.Add(b);
            }
            root.Children.Add(bar);
            var content = new DockPanel();
            var badge = new Border { Width = 34, Height = 34, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 14, 0) };
            badge.Background = kind switch
            {
                "error" => new SolidColorBrush(Color.FromRgb(0xF2, 0x5C, 0x5C)),
                "warn" => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
                _ => Res("AccentPrimaryBrush")
            };
            badge.Child = new TextBlock
            {
                Text = kind switch { "error" => "!", "warn" => "!", "question" => "?", _ => "i" },
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(badge, Dock.Left);
            content.Children.Add(badge);
            var text = new TextBox
            {
                Text = message,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(0),
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalContentAlignment = VerticalAlignment.Top
            };
            text.SetResourceReference(Control.ForegroundProperty, "FgPrimaryBrush");
            text.Template = PlainTextTemplate();
            content.Children.Add(text);
            root.Children.Add(content);
            win.Content = root;
            win.ShowDialog();
            return result;
        }
        private static ControlTemplate PlainTextTemplate()
        {
            var t = new ControlTemplate(typeof(TextBox));
            var sv = new FrameworkElementFactory(typeof(ScrollViewer)) { Name = "PART_ContentHost" };
            t.VisualTree = sv;
            return t;
        }
        public static string? Prompt(string title, string prompt, string initial = "")
        {
            var win = new OlWindow(title, 440, 200) { SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
            string? result = null;
            var root = new StackPanel { Margin = new Thickness(22, 18, 22, 16) };
            root.Children.Add(Label(prompt, "", true));
            var box = TextBox(initial);
            box.Margin = new Thickness(0, 10, 0, 0);
            root.Children.Add(box);
            var ok = Button(T("QT_OK", "OK"), () => { result = box.Text; win.Close(); }, "accent");
            ok.IsDefault = true;
            ok.MinWidth = 90;
            var cancel = Button(T("CANCEL", "Cancel"), () => win.Close());
            cancel.MinWidth = 90;
            var bar = Row(8, cancel, ok);
            bar.HorizontalAlignment = HorizontalAlignment.Right;
            bar.Margin = new Thickness(0, 16, 0, 0);
            root.Children.Add(bar);
            win.Content = root;
            win.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
            win.ShowDialog();
            return result;
        }
        public static string? Choose(string title, string heading, params (string Key, string Label, string Description, string? Icon)[] options)
        {
            var win = new OlWindow(title, 560, 300) { SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize };
            string? result = null;
            var root = new StackPanel { Margin = new Thickness(22, 18, 22, 16) };
            root.Children.Add(Label(heading, "h3", true));
            foreach (var o in options)
            {
                var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var key = o.Key;
                var b = Button(o.Label, () => { result = key; win.Close(); }, "", o.Icon);
                b.HorizontalContentAlignment = HorizontalAlignment.Left;
                grid.Children.Add(b);
                var d = Label(o.Description, "muted", true);
                d.Margin = new Thickness(12, 0, 0, 0);
                Grid.SetColumn(d, 1);
                grid.Children.Add(d);
                root.Children.Add(grid);
            }
            var cancel = Button(T("CANCEL", "Cancel"), () => win.Close());
            cancel.HorizontalAlignment = HorizontalAlignment.Right;
            cancel.MinWidth = 90;
            cancel.Margin = new Thickness(0, 16, 0, 0);
            root.Children.Add(cancel);
            win.Content = root;
            win.ShowDialog();
            return result;
        }
        public static void ShowText(string title, string text)
        {
            var win = new OlWindow(title, 720, 440);
            var root = new DockPanel { Margin = new Thickness(16) };
            var close = Button(T("QT_CLOSE", "Close"), () => win.Close(), "accent");
            var copy = Button(T("QT_COPY", "Copy"), () => Copy(text));
            var bar = Row(8, copy, close);
            bar.HorizontalAlignment = HorizontalAlignment.Right;
            bar.Margin = new Thickness(0, 10, 0, 0);
            DockPanel.SetDock(bar, Dock.Bottom);
            root.Children.Add(bar);
            var box = TextBox(text, null, true);
            box.IsReadOnly = true;
            box.Height = double.NaN;
            box.TextWrapping = TextWrapping.NoWrap;
            box.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            box.SetResourceReference(Control.FontFamilyProperty, "MonospaceFontFamily");
            root.Children.Add(box);
            win.Content = root;
            win.ShowDialog();
        }
    }
}
