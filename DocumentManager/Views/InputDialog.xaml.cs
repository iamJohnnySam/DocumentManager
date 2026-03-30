using System.Windows;

namespace DocumentManager.Views;

public partial class InputDialog : Window
{
    public string InputText => InputBox.Text.Trim();

    public InputDialog(string prompt, string title = "Input")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
