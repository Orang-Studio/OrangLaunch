using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
namespace OrangLauncher.Managers
{
    public static class WindowStyleManager
    {
        public static bool TrySetMicaBackdrop(Window window)
        {
            if (MicaController.IsSupported())
            {
                window.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
                return true;
            }
            return false;
        }
        public static bool TrySetAcrylicBackdrop(Window window)
        {
            if (DesktopAcrylicController.IsSupported())
            {
                window.SystemBackdrop = new DesktopAcrylicBackdrop();
                return true;
            }
            return false;
        }
        public static void SetDefaultBackdrop(Window window)
        {
            if (!TrySetMicaBackdrop(window))
                TrySetAcrylicBackdrop(window);
        }
    }
}