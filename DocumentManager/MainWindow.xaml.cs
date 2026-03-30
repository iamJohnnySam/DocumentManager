using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DocumentManager.Models;
using DocumentManager.ViewModels;
using DocumentManager.Views;

namespace DocumentManager;

/// <summary>
/// Main application window hosting the LDMS interface.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Wire up dialog callbacks
        _viewModel.RequestNewProjectInfo = ShowNewProjectDialog;
        _viewModel.RequestOpenProjectCode = ShowOpenProjectDialog;
        _viewModel.RequestRevisionNotes = ShowRevisionNotesDialog;
        _viewModel.RequestSectionName = ShowSectionNameDialog;
        _viewModel.RequestSnippetInfo = ShowSnippetDialog;
        _viewModel.ShowDiffViewer = ShowDiffWindow;
        _viewModel.RequestBrowseAvailableFiles = ShowFileBrowserWindow;

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(_viewModel.SaveCommand, Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_viewModel.CompileCommand, Key.F5, ModifierKeys.None));
    }

    public MainViewModel ViewModel => _viewModel;

    private (string ProjectCode, string Title, string Author)? ShowNewProjectDialog()
    {
        var dialog = new NewProjectDialog { Owner = this };
        if (dialog.ShowDialog() == true)
            return (dialog.ProjectCode, dialog.ProjectTitle, dialog.Author);
        return null;
    }

    /// <summary>
    /// Shows a dialog listing available project codes under the common root.
    /// The user picks one to open.
    /// </summary>
    private string? ShowOpenProjectDialog()
    {
        _viewModel.RefreshAvailableProjects();
        var codes = _viewModel.AvailableProjectCodes;

        if (codes.Count == 0)
        {
            MessageBox.Show("No projects found in the main files folder.\nCreate a new project first.",
                "Open Project", MessageBoxButton.OK, MessageBoxImage.Information);
            return null;
        }

        // Use a simple input dialog that also shows the list
        var listText = string.Join("\n", codes);
        var dialog = new InputDialog(
            $"Enter the Project Code to open.\n\nAvailable projects:\n{listText}",
            "Open Project")
        { Owner = this };

        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    private string? ShowRevisionNotesDialog()
    {
        var dialog = new RevisionNotesDialog { Owner = this };
        return dialog.ShowDialog() == true ? dialog.Notes : null;
    }

    private string? ShowSectionNameDialog()
    {
        var dialog = new InputDialog("Enter section name:", "Add Section") { Owner = this };
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    private (string Name, string Content)? ShowSnippetDialog()
    {
        var dialog = new SnippetDialog { Owner = this };
        if (dialog.ShowDialog() == true)
            return (dialog.SnippetName, dialog.SnippetContent);
        return null;
    }

    private void ShowDiffWindow(string title, string oldContent, string newContent)
    {
        var window = new DiffViewerWindow(title, oldContent, newContent) { Owner = this };
        window.Show();
    }

    private List<(string Name, int LatestRevision)>? ShowFileBrowserWindow(
        string sharedSectionsPath, IReadOnlySet<string> alreadyIncluded)
    {
        var window = new FileBrowserWindow(sharedSectionsPath, alreadyIncluded) { Owner = this };
        return window.ShowDialog() == true ? window.SelectedSections : null;
    }

    private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView tree && tree.SelectedItem is ProjectTreeNode node)
            _viewModel.ProjectTree.RequestOpenFile(node);
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ProjectTreeNode node)
            _viewModel.ProjectTree.SelectedNode = node;
    }

    private void SearchResult_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is SearchResult result)
            _ = _viewModel.Editor.OpenFileAsync(result.FilePath);
    }
}

/// <summary>
/// Converter used in the menu to show which compiler is selected.
/// </summary>
public class StringMatchConverter : IValueConverter
{
    public static readonly StringMatchConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return parameter?.ToString() ?? string.Empty;
    }
}