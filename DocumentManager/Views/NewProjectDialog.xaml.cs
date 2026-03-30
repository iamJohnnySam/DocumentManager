using System.Windows;

namespace DocumentManager.Views;

public partial class NewProjectDialog : Window
{
    public string ProjectCode => ProjectCodeBox.Text.Trim();
    public string ProjectTitle => TitleBox.Text.Trim();
    public string Author => AuthorBox.Text.Trim();

    public NewProjectDialog()
    {
        InitializeComponent();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectCode))
        {
            MessageBox.Show("Project Code is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(ProjectTitle))
        {
            MessageBox.Show("Title is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
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
