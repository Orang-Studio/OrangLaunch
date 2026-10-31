using System.IO;
using System.Windows;
using System.Windows.Controls;
using OrangLauncher.Controls;
using static OrangLauncher.UiKit;
namespace OrangLauncher
{
    public class SkinUploadDialog : OlWindow
    {
        public bool Slim { get; private set; }
        public SkinUploadDialog(string path, bool slim) : base(T("QT_UPLOAD_SKIN", "Upload skin..."), 460, 560)
        {
            Slim = slim;
            var bytes = File.ReadAllBytes(path);
            var root = new DockPanel { Margin = new Thickness(20, 16, 20, 16) };
            var head = Label(T("QT_SKIN_MODEL_Q", "Which arm model does this skin use?"), "h3", true);
            DockPanel.SetDock(head, Dock.Top);
            root.Children.Add(head);
            var file = Label(Path.GetFileName(path), "muted");
            file.Margin = new Thickness(0, 2, 0, 8);
            DockPanel.SetDock(file, Dock.Top);
            root.Children.Add(file);
            var viewer = new SkinViewer3D { Width = 220, Height = 300, ToolTip = T("QT_DRAG_ROTATE", "Drag to rotate"), Cursor = System.Windows.Input.Cursors.SizeAll };
            var classic = new RadioButton { Content = T("QT_SKIN_CLASSIC", "Classic (wide arms)"), GroupName = "model", IsChecked = !slim, Margin = new Thickness(0, 0, 16, 0) };
            var slimRb = new RadioButton { Content = T("QT_SKIN_SLIM", "Slim (thin arms)"), GroupName = "model", IsChecked = slim };
            classic.Checked += (_, _) => { Slim = false; viewer.LoadSkin(bytes, false); };
            slimRb.Checked += (_, _) => { Slim = true; viewer.LoadSkin(bytes, true); };
            var models = Row(0, classic, slimRb);
            models.HorizontalAlignment = HorizontalAlignment.Center;
            models.Margin = new Thickness(0, 10, 0, 0);
            var upload = Button(T("QT_UPLOAD", "Upload"), () => DialogResult = true, "accent", "user");
            upload.IsDefault = true;
            var bar = Row(8, Button(T("CANCEL", "Cancel"), Close), upload);
            bar.HorizontalAlignment = HorizontalAlignment.Right;
            bar.Margin = new Thickness(0, 14, 0, 0);
            DockPanel.SetDock(bar, Dock.Bottom);
            root.Children.Add(bar);
            DockPanel.SetDock(models, Dock.Bottom);
            root.Children.Add(models);
            var frame = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Center, Child = viewer };
            frame.SetResourceReference(Border.BackgroundProperty, "BgSecondaryBrush");
            frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            root.Children.Add(frame);
            Content = root;
            Loaded += (_, _) => viewer.LoadSkin(bytes, slim);
        }
    }
}