using Microsoft.Web.WebView2.Core;

namespace OrangLauncher.Managers
{
    /// <summary>
    /// Creates the WebView2 environment with an explicit per-user data folder.
    /// Unpackaged apps that let WebView2 pick its own folder can fail with
    /// "Error in the DLL" when the default location is not usable.
    /// </summary>
    public static class WebView2Helper
    {
        private static CoreWebView2Environment? _environment;

        public static async Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            if (_environment != null) return _environment;
            // null (not "") selects the installed Evergreen runtime; an empty string
            // is treated as a browser folder path and fails with "Error in the DLL".
            // The user data folder must be LOCAL AppData: WebView2 fails with
            // ERROR_DLL_INIT_FAILED on roaming/redirected profiles.
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OrangStudio", "OrangLauncher", "webview2");
            var attempts = new List<string>();
            try { attempts.Add($"runtime={CoreWebView2Environment.GetAvailableBrowserVersionString()}"); }
            catch (Exception ex) { attempts.Add($"runtime-check: {ex.Message}"); }
            try
            {
                Directory.CreateDirectory(dataDir);
                _environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    null, dataDir, new CoreWebView2EnvironmentOptions());
                return _environment;
            }
            catch (Exception ex) { attempts.Add($"A(dataDir+options): 0x{ex.HResult:X8} {ex.Message}"); }
            try
            {
                _environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, dataDir, null);
                return _environment;
            }
            catch (Exception ex) { attempts.Add($"B(dataDir,null-options): 0x{ex.HResult:X8} {ex.Message}"); }
            try
            {
                _environment = await CoreWebView2Environment.CreateAsync();
                return _environment;
            }
            catch (Exception ex) { attempts.Add($"C(defaults): 0x{ex.HResult:X8} {ex.Message}"); }
            try
            {
                // Second chance: a writable temp profile in case the data dir is locked
                // by another elevation level or blocked by folder permissions.
                var tempDir = Path.Combine(Path.GetTempPath(), "OrangLauncher", "webview2");
                Directory.CreateDirectory(tempDir);
                _environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    null, tempDir, new CoreWebView2EnvironmentOptions());
                return _environment;
            }
            catch (Exception ex) { attempts.Add($"D(tempDir+options): 0x{ex.HResult:X8} {ex.Message}"); }
            throw new Exception(string.Join(" | ", attempts));
        }

        /// <summary>True when a WebView2 Evergreen runtime is installed.</summary>
        public static bool IsRuntimeAvailable()
        {
            try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
            catch { return false; }
        }
    }
}
