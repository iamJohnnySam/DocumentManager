using System.Windows;

namespace DocumentManager.Views;

public partial class SnippetDialog : Window
{
    public string SnippetName => NameBox.Text.Trim();
    public string SnippetContent => ContentBox.Text;

    public SnippetDialog()
    {
        InitializeComponent();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SnippetName))
        {
            MessageBox.Show("Snippet name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
