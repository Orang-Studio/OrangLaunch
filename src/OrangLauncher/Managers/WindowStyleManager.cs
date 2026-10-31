using System;
using System.Runtime.InteropServices;
namespace OrangLauncher.Managers
{
    public static class WindowStyleManager
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
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
}   }   }