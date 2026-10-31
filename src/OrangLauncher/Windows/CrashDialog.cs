using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static OrangLauncher.UiKit;
namespace OrangLauncher
{
    public class CrashDialog : OlWindow
    {
        public CrashDialog(int exitCode, string reportPath, string summary) : base(T("QT_CRASHED", "Minecraft Crashed"), 720, 460)
        {
            var root = new DockPanel { Margin = new Thickness(18, 14, 18, 14) };
            var title = Label(T("QT_EXITED_WITH", "Minecraft exited with code {code}", ("code", exitCode)), "h2");
            title.Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0x5C, 0x5C));
            DockPanel.SetDock(title, Dock.Top);
            root.Children.Add(title);
            var sub = Label(T("QT_LATEST_CRASH_REPORT", "Latest crash report: {name}", ("name", Path.GetFileName(reportPath))), "muted");
            sub.Margin = new Thickness(0, 2, 0, 8);
            DockPanel.SetDock(sub, Dock.Top);
            root.Children.Add(sub);
            var bar = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            var close = Button(T("QT_CLOSE", "Close"), Close, "accent");
            DockPanel.SetDock(close, Dock.Right);
            bar.Children.Add(close);
            bar.Children.Add(Bar(
                Button(T("QT_OPEN_REPORT", "Open full report"), () => OpenInEditor(reportPath)),
                Button(T("QT_OPEN_CRASH_FOLDER", "Open crash-reports folder"), () => OpenPath(Path.GetDirectoryName(reportPath)!)),
                Button(T("QT_SHOW_LOG", "Show launcher log"), () => { MainWindow.Current?.ShowPage("logs"); Close(); })));
            DockPanel.SetDock(bar, Dock.Bottom);
            root.Children.Add(bar);
            var text = TextBox(summary, null, true);
            text.IsReadOnly = true;
            text.Height = double.NaN;
            text.TextWrapping = TextWrapping.NoWrap;
            text.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            text.SetResourceReference(FontFamilyProperty, "MonospaceFontFamily");
            root.Children.Add(text);
            Content = root;
        }
    }
}