namespace OrangLauncher.Managers
{
    /// <summary>
    /// UI-agnostic file picking. Core code (ModManager, ResourceManager) calls this;
    /// each UI entry point installs its own implementation (Win32 OpenFileDialog for
    /// WPF, WinRT FileOpenPicker for WinUI).
    /// </summary>
    public static class FilePicker
    {
        /// <summary>(title, extensions like ".jar") → selected full paths, or null if cancelled.</summary>
        public static Func<string, string[], Task<string[]?>>? PickMultiple { get; set; }

        public static Task<string[]?> PickMultipleFilesAsync(string title, params string[] extensions)
        {
            var picker = PickMultiple;
            return picker == null ? Task.FromResult<string[]?>(null) : picker(title, extensions);
        }
    }
}
