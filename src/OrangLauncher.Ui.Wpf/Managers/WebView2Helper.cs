using System;
using System.IO;
using Microsoft.Web.WebView2.Wpf;

namespace OrangLauncher.Managers
{
    /// <summary>
    /// Gives every WPF WebView2 control an explicit user data folder in LOCAL
    /// AppData. Without it the control writes next to the exe, which fails for
    /// installs under Program Files, and roaming profiles trigger
    /// "Error in the DLL" (ERROR_DLL_INIT_FAILED) inside the WebView2 runtime.
    /// </summary>
    public static class WebView2Helper
    {
        public static string UserDataFolder
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrangStudio", "OrangLauncher", "webview2");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Call before the control initializes (right after InitializeComponent).</summary>
        public static void Prepare(WebView2 webView)
        {
            try
            {
                webView.CreationProperties ??= new CoreWebView2CreationProperties();
                webView.CreationProperties.UserDataFolder = UserDataFolder;
            }
            catch { }
        }
    }
}
