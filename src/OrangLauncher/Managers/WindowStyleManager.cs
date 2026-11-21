using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace OrangLauncher.Managers
{
    public static class WindowStyleManager
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subIdList);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        public static void SetDarkTitleBar(IntPtr hwnd, bool dark)
        {
            try
            {
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref v, sizeof(int));
            }
            catch { }
        }
        public static void ApplyTitleBar(System.Windows.Window window)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero) SetDarkTitleBar(hwnd, ThemeManager.Current != "light");
        }
        public static void ApplyToExternalWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                if (LauncherSettings.ModernTitleBar)
                { SetDarkTitleBar(hwnd, ThemeManager.Current != "light"); }
                else
                {
                    SetWindowTheme(hwnd, " ", " ");
                    int cornerPref = 1;
                    DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));
                    SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0020);
                }
            }
            catch { }
        }
        public static void WatchAndApplyProcessWindow(Process process, CancellationToken ct)
        {
            _ = Task.Run(async () =>
            {
                IntPtr appliedHwnd = IntPtr.Zero;
                for (int i = 0; i < 120 && !ct.IsCancellationRequested; i++)
                {
                    try
                    {
                        if (process.HasExited) break;
                        process.Refresh();
                        var hwnd = process.MainWindowHandle;
                        if (hwnd != IntPtr.Zero && hwnd != appliedHwnd)
                        {
                            appliedHwnd = hwnd;
                            ApplyToExternalWindow(hwnd);
                            await Task.Delay(500, ct);
                            ApplyToExternalWindow(hwnd);
                        }
                    }
                    catch { break; }
                    try { await Task.Delay(250, ct); } catch { break; }
                }
            }, ct);
        }
    }
}