using System.Windows.Markup;
namespace OrangLauncher.Managers
{
    [MarkupExtensionReturnType(typeof(string))]
    public class TExtension : MarkupExtension
    {
        public string Key { get; set; } = "";
        public string Default { get; set; } = "";
        public TExtension() { }
        public TExtension(string key, string @default)
        {
            Key = key;
            Default = @default;
        }
        public override object ProvideValue(IServiceProvider serviceProvider) => LocalizationManager.GetString(Key, Default);
    }
}
