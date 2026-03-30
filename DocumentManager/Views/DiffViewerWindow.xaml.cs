using System.Windows;

namespace DocumentManager.Views;

public partial class DiffViewerWindow : Window
{
    public DiffViewerWindow(string title, string oldContent, string newContent)
    {
        InitializeComponent();
        HeaderText.Text = title;
        OldTextBox.Text = oldContent;
        NewTextBox.Text = newContent;
    }
}
