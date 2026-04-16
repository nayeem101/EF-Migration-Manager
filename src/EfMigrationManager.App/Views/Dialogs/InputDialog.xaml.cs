namespace EfMigrationManager.App.Views.Dialogs;

using System.Windows;

public partial class InputDialog
{
    public string Value { get; private set; } = string.Empty;
    public Func<string, string?>? Validator { get; set; }

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title            = title;
        PromptText.Text  = prompt;
        InputBox.Text    = defaultValue;
        InputBox.Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text;
        if (Validator is not null)
        {
            var err = Validator(text);
            if (err is not null)
            {
                ErrorText.Text = err;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
        }
        Value = text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
