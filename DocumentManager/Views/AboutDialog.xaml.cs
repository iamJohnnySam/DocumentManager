using System.Windows;

namespace DocumentManager.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void GitHubLink_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/iamJohnnySam/DocumentManager",
            UseShellExecute = true
        });
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
