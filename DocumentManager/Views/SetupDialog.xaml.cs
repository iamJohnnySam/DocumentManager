using System.Windows;
using Microsoft.Win32;

namespace DocumentManager.Views;

/// <summary>
/// First-run dialog that asks the user to choose the common root folder
/// where shared sections, common images, and all projects are stored.
/// </summary>
public partial class SetupDialog : Window
{
    public string SelectedRootPath => RootPathBox.Text.Trim();

    public SetupDialog(string defaultPath = "")
    {
        InitializeComponent();
        RootPathBox.Text = string.IsNullOrEmpty(defaultPath)
            ? System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LaTeXDocuments")
            : defaultPath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select Main Files Folder" };
        if (dialog.ShowDialog() == true)
            RootPathBox.Text = dialog.FolderName;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SelectedRootPath))
        {
            MessageBox.Show("Please select a folder.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
