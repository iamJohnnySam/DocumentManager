using System.Windows;

namespace DocumentManager.Views;

public partial class RevisionNotesDialog : Window
{
    public string Notes => NotesBox.Text.Trim();
    public bool IsNewRevision { get; private set; } = true;

    public RevisionNotesDialog()
    {
        InitializeComponent();
    }

    public RevisionNotesDialog(string existingNotes) : this()
    {
        if (!string.IsNullOrEmpty(existingNotes))
            NotesBox.Text = existingNotes;
    }

    private void NewRevision_Click(object sender, RoutedEventArgs e)
    {
        IsNewRevision = true;
        DialogResult = true;
        Close();
    }

    private void SaveInPlace_Click(object sender, RoutedEventArgs e)
    {
        IsNewRevision = false;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
