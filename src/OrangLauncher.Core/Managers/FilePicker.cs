namespace OrangLauncher.Managers
{
    public static class FilePicker
    {
        public static Func<string, string[], Task<string[]?>>? PickMultiple { get; set; }

        public static Task<string[]?> PickMultipleFilesAsync(string title, params string[] extensions)
        {
            var picker = PickMultiple;
            return picker == null ? Task.FromResult<string[]?>(null) : picker(title, extensions);
        }
    }
}