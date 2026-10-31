using System.Windows;
namespace OrangLauncher
{
    public partial class AddTextDialog : Window
    {
        public string? ResultText { get; private set; }
        public AddTextDialog(string prompt, string title, string defaultValue = "")
        {
            InitializeComponent();
            OlChrome.Apply(this);
            Title = title;
            PromptText.Text = prompt;
            OkButton.Content = Managers.LocalizationManager.GetString("QT_OK", "OK");
            CancelButton.Content = Managers.LocalizationManager.GetString("CANCEL", "Cancel");
            InputTextBox.Text = defaultValue;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText = InputTextBox.Text;
            DialogResult = true;
            Close();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}