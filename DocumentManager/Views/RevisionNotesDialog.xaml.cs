using System.Windows;

namespace DocumentManager.Views;

public partial class RevisionNotesDialog : Window
{
    public string Notes => NotesBox.Text.Trim();

    public RevisionNotesDialog()
    {
        InitializeComponent();
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
