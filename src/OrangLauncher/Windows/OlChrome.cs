using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using OrangLauncher.Managers;
namespace OrangLauncher
{
    public static class OlChrome
    {
        private const int WM_SIZE = 0x0005, WM_SETTEXT = 0x000C, WM_WINDOWPOSCHANGED = 0x0047;
        private const int WM_NCHITTEST = 0x0084, WM_NCPAINT = 0x0085, WM_NCACTIVATE = 0x0086;
        private const int WM_NCMOUSEMOVE = 0x00A0, WM_NCLBUTTONDOWN = 0x00A1, WM_NCLBUTTONUP = 0x00A2, WM_NCLBUTTONDBLCLK = 0x00A3, WM_NCMOUSELEAVE = 0x02A2;
        private const int WM_GETTITLEBARINFOEX = 0x033F;
        private const int HTHELP = 21;
        private const int DFC_CAPTION = 1, DFCS_CAPTIONHELP = 0x0004, DFCS_PUSHED = 0x0200;
        private const int HTCLIENT = 1, HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        private const int ResizeGrip = 5;
        private const int FrameColor = 0x808080;
        public static readonly Brush FrameBrush = CreateFrameBrush();
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct TITLEBARINFOEX
        {
            public int Size;
            public RECT TitleBar;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public int[] State;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public RECT[] Buttons;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct TRACKMOUSEEVENT { public int Size; public uint Flags; public IntPtr Hwnd; public uint HoverTime; }
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref TITLEBARINFOEX info);
        [DllImport("user32.dll")]
        private static extern bool DrawFrameControl(IntPtr dc, ref RECT rect, int type, int state);
        [DllImport("user32.dll")]
        private static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT tme);
        private sealed class HelpButton
        {
            public Action OnClick = () => { };
            public bool Pressed;
        }
        private static readonly Dictionary<Window, HelpButton> PendingHelp = new();
        private static readonly Dictionary<IntPtr, HelpButton> HelpButtons = new();
        public static void AddHelpButton(Window window, Action onClick) => PendingHelp[window] = new HelpButton { OnClick = onClick };
        private static bool HelpRect(IntPtr hwnd, out RECT rect)
        {
            rect = default;
            if (!GetWindowRect(hwnd, out var w)) return false;
            var info = new TITLEBARINFOEX { Size = Marshal.SizeOf<TITLEBARINFOEX>(), State = new int[6], Buttons = new RECT[6] };
            SendMessage(hwnd, WM_GETTITLEBARINFOEX, IntPtr.Zero, ref info);
            var anchor = info.Buttons[2];
            if (anchor.Right <= anchor.Left) anchor = info.Buttons[5];
            if (anchor.Right <= anchor.Left) return false;
            int left = anchor.Left + 3, top = anchor.Top + 3, right = anchor.Right - 1, bottom = anchor.Bottom - 3;
            int width = right - left;
            rect = new RECT { Left = left - 2 - width - w.Left, Top = top - w.Top, Right = left - 2 - w.Left, Bottom = bottom - w.Top };
            return true;
        }
        private static void PaintHelp(IntPtr hwnd)
        {
            if (!HelpButtons.TryGetValue(hwnd, out var help) || !HelpRect(hwnd, out var rect)) return;
            var dc = GetWindowDC(hwnd);
            if (dc == IntPtr.Zero) return;
            DrawFrameControl(dc, ref rect, DFC_CAPTION, DFCS_CAPTIONHELP | (help.Pressed ? DFCS_PUSHED : 0));
            ReleaseDC(hwnd, dc);
        }
        private static bool OverHelp(IntPtr hwnd, IntPtr lParam)
        {
            if (!HelpButtons.ContainsKey(hwnd) || !HelpRect(hwnd, out var rect) || !GetWindowRect(hwnd, out var w)) return false;
            int x = (short)(lParam.ToInt64() & 0xFFFF) - w.Left, y = (short)((lParam.ToInt64() >> 16) & 0xFFFF) - w.Top;
            return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
        }
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subIdList);
        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(int color);
        [DllImport("user32.dll")]
        private static extern int FillRect(IntPtr dc, ref RECT rect, IntPtr brush);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);
        private static Brush CreateFrameBrush()
        {
            var b = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
            b.Freeze();
            return b;
        }
        public static void Apply(Window window)
        {
            if (window.WindowStyle == WindowStyle.ToolWindow) window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.SourceInitialized += (_, _) =>
            {
                if (LauncherSettings.ModernTitleBar) InitModern(window);
                else Init(window);
            };
        }
        private const int DWMWA_CAPTION_BUTTON_BOUNDS = 5;
        private const double ModernCaption = 32;
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
        private static void InitModern(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            PendingHelp.Remove(window, out var helpButton);
            WindowChrome.SetWindowChrome(window, new WindowChrome
            {
                CaptionHeight = ModernCaption,
                GlassFrameThickness = new Thickness(0, ModernCaption, 0, 0),
                ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
                UseAeroCaptionButtons = true,
                CornerRadius = new CornerRadius(0)
            });
            if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
            window.Background = Brushes.Transparent;
            var content = window.Content as UIElement;
            window.Content = null;
            var host = new Border { Child = content };
            host.SetResourceReference(Border.BackgroundProperty, "BgPrimaryBrush");
            Grid.SetRow(host, 1);
            var icon = new Image { Width = 16, Height = 16, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            icon.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding(nameof(Window.Icon)) { Source = window });
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            var title = new TextBlock { FontSize = 12, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Window.Title)) { Source = window });
            var spacer = new FrameworkElement();
            DockPanel.SetDock(spacer, Dock.Right);
            var bar = new DockPanel { Background = Brushes.Transparent };
            bar.Children.Add(spacer);
            TextBlock? glyph = null;
            Border? help = null;
            if (helpButton != null)
            {
                glyph = new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                help = new Border { Width = 46, Background = Brushes.Transparent, Child = glyph, ToolTip = "Help" };
                DockPanel.SetDock(help, Dock.Right);
                WindowChrome.SetIsHitTestVisibleInChrome(help, true);
                help.MouseEnter += (_, _) => help.Background = new SolidColorBrush(ThemeManager.Current == "light" ? Color.FromArgb(0x18, 0, 0, 0) : Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
                help.MouseLeave += (_, _) => help.Background = Brushes.Transparent;
                help.MouseLeftButtonDown += (_, e) => { help.CaptureMouse(); e.Handled = true; };
                help.MouseLeftButtonUp += (_, e) =>
                {
                    var inside = help.IsMouseCaptured && help.IsMouseOver;
                    help.ReleaseMouseCapture();
                    e.Handled = true;
                    if (inside) helpButton.OnClick();
                };
                bar.Children.Add(help);
            }
            bar.Children.Add(icon);
            bar.Children.Add(title);
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ModernCaption) });
            root.RowDefinitions.Add(new RowDefinition());
            root.Children.Add(bar);
            root.Children.Add(host);
            window.Content = root;
            void Theme(string _ = "")
            {
                var dark = ThemeManager.Current != "light";
                WindowStyleManager.SetDarkTitleBar(hwnd, dark);
                var fg = dark ? Brushes.White : Brushes.Black;
                title.Foreground = fg;
                if (glyph != null) glyph.Foreground = fg;
            }
            void Fit()
            {
                var dpi = VisualTreeHelper.GetDpi(window);
                var maximized = window.WindowState == WindowState.Maximized;
                var edge = SystemParameters.WindowResizeBorderThickness;
                root.Margin = maximized ? new Thickness(edge.Left + 4, edge.Top + 4, edge.Right + 4, edge.Bottom + 4) : new Thickness(0);
                if (DwmGetWindowAttribute(hwnd, DWMWA_CAPTION_BUTTON_BOUNDS, out var r, Marshal.SizeOf<RECT>()) != 0 || r.Right <= r.Left) return;
                spacer.Width = (r.Right - r.Left) / dpi.DpiScaleX;
                var top = Math.Max(0, r.Top / dpi.DpiScaleY - root.Margin.Top);
                var height = Math.Min(ModernCaption, (r.Bottom - r.Top) / dpi.DpiScaleY);
                root.RowDefinitions[0].Height = new GridLength(maximized ? top + height : ModernCaption);
                if (help != null)
                {
                    help.VerticalAlignment = VerticalAlignment.Top;
                    help.Margin = new Thickness(0, top, 0, 0);
                    help.Height = height;
                }
            }
            Action<string> onTheme = t => window.Dispatcher.BeginInvoke(() => Theme(t));
            ThemeManager.ThemeChanged += onTheme;
            window.Closed += (_, _) => ThemeManager.ThemeChanged -= onTheme;
            window.Activated += (_, _) => { title.Opacity = 1; if (glyph != null) glyph.Opacity = 1; };
            window.Deactivated += (_, _) => { title.Opacity = 0.55; if (glyph != null) glyph.Opacity = 0.55; };
            window.StateChanged += (_, _) => Fit();
            window.SizeChanged += (_, _) => Fit();
            window.DpiChanged += (_, _) => Fit();
            window.Loaded += (_, _) => Fit();
            Theme();
            Fit();
        }
        private static bool Layout(IntPtr hwnd, out RECT window, out int frame, out int captionBottom)
        {
            frame = 0;
            captionBottom = 0;
            if (!GetWindowRect(hwnd, out window)) return false;
            var origin = new POINT();
            if (!ClientToScreen(hwnd, ref origin)) return false;
            frame = Math.Max(0, origin.X - window.Left);
            captionBottom = origin.Y - window.Top;
            return true;
        }
        private static readonly Dictionary<IntPtr, (int W, int H, int F, bool Zoomed)> Regions = new();
        private static readonly HashSet<IntPtr> Updating = new();
        private static void UpdateRegion(IntPtr hwnd)
        {
            if (!Updating.Add(hwnd)) return;
            try
            {
                var zoomed = IsZoomed(hwnd);
                if (!Layout(hwnd, out var w, out var f, out _)) return;
                int width = w.Right - w.Left, height = w.Bottom - w.Top;
                var key = (width, height, f, zoomed);
                if (Regions.TryGetValue(hwnd, out var last) && last == key) return;
                Regions[hwnd] = key;
                SetWindowRgn(hwnd, zoomed ? IntPtr.Zero : CreateRectRgn(f, f, width - f, height - f), true);
            }
            finally { Updating.Remove(hwnd); }
        }
        private static void PaintCaptionFrame(IntPtr hwnd)
        {
            if (IsZoomed(hwnd) || !Layout(hwnd, out var w, out var f, out var captionBottom)) return;
            int width = w.Right - w.Left;
            var dc = GetWindowDC(hwnd);
            if (dc == IntPtr.Zero) return;
            var brush = CreateSolidBrush(FrameColor);
            var top = new RECT { Left = f, Top = f, Right = width - f, Bottom = f + 1 };
            var left = new RECT { Left = f, Top = f, Right = f + 1, Bottom = captionBottom };
            var right = new RECT { Left = width - f - 1, Top = f, Right = width - f, Bottom = captionBottom };
            FillRect(dc, ref top, brush);
            FillRect(dc, ref left, brush);
            FillRect(dc, ref right, brush);
            DeleteObject(brush);
            ReleaseDC(hwnd, dc);
        }
        private static void Init(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            try { SetWindowTheme(hwnd, " ", " "); } catch { }
            var resizable = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
            if (PendingHelp.Remove(window, out var helpButton)) HelpButtons[hwnd] = helpButton;
            var border = new Border { BorderBrush = FrameBrush, SnapsToDevicePixels = true };
            var content = window.Content as UIElement;
            window.Content = null;
            border.Child = content;
            window.Content = border;
            void Fit() => border.BorderThickness = window.WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1, 0, 1, 1);
            window.StateChanged += (_, _) => Fit();
            Fit();
            HwndSource.FromHwnd(hwnd)?.AddHook((IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                switch (msg)
                {
                    case WM_SIZE:
                    case WM_WINDOWPOSCHANGED:
                        UpdateRegion(h);
                        return IntPtr.Zero;
                    case WM_NCPAINT:
                    case WM_NCACTIVATE:
                    case WM_SETTEXT:
                    {
                        var result = DefWindowProc(h, msg, wParam, lParam);
                        PaintCaptionFrame(h);
                        PaintHelp(h);
                        handled = true;
                        return result;
                    }
                    case WM_NCHITTEST when OverHelp(h, lParam):
                        handled = true;
                        return new IntPtr(HTHELP);
                    case WM_NCLBUTTONDOWN or WM_NCLBUTTONDBLCLK when wParam.ToInt32() == HTHELP && HelpButtons.TryGetValue(h, out var down):
                    {
                        down.Pressed = true;
                        PaintHelp(h);
                        var tme = new TRACKMOUSEEVENT { Size = Marshal.SizeOf<TRACKMOUSEEVENT>(), Flags = 0x0002 | 0x0010, Hwnd = h };
                        TrackMouseEvent(ref tme);
                        handled = true;
                        return IntPtr.Zero;
                    }
                    case WM_NCLBUTTONUP when wParam.ToInt32() == HTHELP && HelpButtons.TryGetValue(h, out var up):
                    {
                        var wasPressed = up.Pressed;
                        up.Pressed = false;
                        PaintHelp(h);
                        handled = true;
                        if (wasPressed) up.OnClick();
                        return IntPtr.Zero;
                    }
                    case WM_NCMOUSEMOVE when HelpButtons.TryGetValue(h, out var move) && move.Pressed && wParam.ToInt32() != HTHELP:
                    case WM_NCMOUSELEAVE when HelpButtons.TryGetValue(h, out move) && move.Pressed:
                        move.Pressed = false;
                        PaintHelp(h);
                        return IntPtr.Zero;
                    case WM_NCHITTEST when resizable && !IsZoomed(h):
                    {
                        var hit = DefWindowProc(h, msg, wParam, lParam).ToInt32();
                        if (hit is not (HTCLIENT or HTCAPTION or HTLEFT or HTRIGHT or HTTOP or HTBOTTOM or HTTOPLEFT or HTTOPRIGHT or HTBOTTOMLEFT or HTBOTTOMRIGHT)) return IntPtr.Zero;
                        if (!Layout(h, out var r, out var f, out _)) return IntPtr.Zero;
                        int x = (short)(lParam.ToInt64() & 0xFFFF), y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                        bool left = x < r.Left + f + ResizeGrip, right = x >= r.Right - f - ResizeGrip;
                        bool top = y < r.Top + f + ResizeGrip, bottom = y >= r.Bottom - f - ResizeGrip;
                        int code = top && left ? HTTOPLEFT : top && right ? HTTOPRIGHT : bottom && left ? HTBOTTOMLEFT : bottom && right ? HTBOTTOMRIGHT
                            : left ? HTLEFT : right ? HTRIGHT : top ? HTTOP : bottom ? HTBOTTOM : hit;
                        handled = true;
                        return new IntPtr(code);
                    }
                }
                return IntPtr.Zero;
            });
            window.Closed += (_, _) => { Regions.Remove(hwnd); HelpButtons.Remove(hwnd); };
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0020);
            UpdateRegion(hwnd);
        }
    }
}