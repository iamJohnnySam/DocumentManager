using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DocumentManager.Models;
using DocumentManager.ViewModels;
using DocumentManager.Views;
using Microsoft.Win32;

namespace DocumentManager;

/// <summary>
/// Main application window hosting the LDMS interface.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private TextBox? _activeEditorTextBox;
    private bool _isPdfPanelOpen;
    private bool _webViewInitialized;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Wire up dialog callbacks
        _viewModel.RequestNewProjectInfo = ShowNewProjectDialog;
        _viewModel.RequestOpenProjectCode = ShowOpenProjectDialog;
        _viewModel.RequestSectionName = ShowSectionNameDialog;
        _viewModel.RequestSnippetInfo = ShowSnippetDialog;
        _viewModel.ShowDiffViewer = ShowDiffWindow;
        _viewModel.RequestBrowseAvailableFiles = ShowFileBrowserWindow;
        _viewModel.Editor.RequestRevisionNotes = ShowRevisionNotesDialog;
        _viewModel.Editor.InsertTextAtCursorCallback = InsertTextAtEditorCursor;
        _viewModel.RequestImageFilePath = ShowOpenImageDialog;
        _viewModel.RequestTemplateName = ShowTemplateNameDialog;
        _viewModel.ShowAboutDialog = ShowAboutDialogWindow;

        // Watch for PDF compilation results
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(_viewModel.SaveCommand, Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_viewModel.CompileCommand, Key.F5, ModifierKeys.None));
    }

    public MainViewModel ViewModel => _viewModel;

    private (string ProjectCode, string Title, string Author, string TemplateName)? ShowNewProjectDialog(List<string> templates)
    {
        var dialog = new NewProjectDialog(templates) { Owner = this };
        if (dialog.ShowDialog() == true)
            return (dialog.ProjectCode, dialog.ProjectTitle, dialog.Author, dialog.SelectedTemplate);
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

    private (string Notes, bool IsNewRevision)? ShowRevisionNotesDialog(string sectionName, string existingNotes)
    {
        var dialog = new RevisionNotesDialog(existingNotes)
        {
            Owner = this,
            Title = $"Revision Notes — {sectionName}"
        };
        return dialog.ShowDialog() == true ? (dialog.Notes, dialog.IsNewRevision) : null;
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

    private void EditorTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
            _activeEditorTextBox = textBox;
    }

    private void InsertTextAtEditorCursor(string text)
    {
        if (_activeEditorTextBox is null || _viewModel.Editor.SelectedTab is null)
        {
            _viewModel.Editor.InsertText(text);
            return;
        }

        var content = _viewModel.Editor.SelectedTab.Content ?? "";
        var caretIndex = _activeEditorTextBox.CaretIndex;
        if (caretIndex < 0 || caretIndex > content.Length)
            caretIndex = content.Length;

        _viewModel.Editor.SelectedTab.Content = content.Insert(caretIndex, text);

        // Restore caret to end of inserted text
        Dispatcher.BeginInvoke(() =>
        {
            _activeEditorTextBox.CaretIndex = caretIndex + text.Length;
            _activeEditorTextBox.Focus();
        });
    }

    private string? ShowOpenImageDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select an image to import",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.pdf;*.eps;*.svg|All files|*.*"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private string? ShowTemplateNameDialog()
    {
        var dialog = new InputDialog("Enter a name for the template:", "Save as Template") { Owner = this };
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    private void ShowAboutDialogWindow()
    {
        var dialog = new AboutDialog { Owner = this };
        dialog.ShowDialog();
    }

    private void OutputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.ScrollToEnd();
    }

    // ─── PDF Preview ─────────────────────────────────────────────────

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CompiledPdfPath))
        {
            if (_viewModel.IsPdfPreviewVisible)
            {
                ShowPdfPanel(true);
                _ = NavigateToPdfAsync();
            }
        }
    }

    private void PdfToggle_Click(object sender, RoutedEventArgs e)
    {
        var show = PdfToggleButton.IsChecked == true && _viewModel.IsPdfPreviewVisible;
        ShowPdfPanel(show);
        if (show)
            _ = NavigateToPdfAsync();
    }

    private void ReloadPdf_Click(object sender, RoutedEventArgs e)
    {
        _ = NavigateToPdfAsync();
    }

    private void ShowPdfPanel(bool show)
    {
        _isPdfPanelOpen = show;
        if (show)
        {
            PdfPreviewColumn.Width = new GridLength(1, GridUnitType.Star);
            PdfSplitter.Visibility = Visibility.Visible;
            PdfPreviewPanel.Visibility = Visibility.Visible;
            PdfToggleButton.IsChecked = true;
        }
        else
        {
            PdfPreviewColumn.Width = new GridLength(0);
            PdfSplitter.Visibility = Visibility.Collapsed;
            PdfPreviewPanel.Visibility = Visibility.Collapsed;
            PdfToggleButton.IsChecked = false;
        }
    }

    private async Task NavigateToPdfAsync()
    {
        var pdfPath = _viewModel.CompiledPdfPath;
        if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath)) return;

        try
        {
            if (!_webViewInitialized)
            {
                await PdfWebView.EnsureCoreWebView2Async();
                _webViewInitialized = true;
            }

            // Use a cache-busting query string to force reload
            var uri = new Uri(pdfPath);
            PdfWebView.CoreWebView2.Navigate(uri.AbsoluteUri + "?t=" + DateTime.Now.Ticks);
        }
        catch (Exception ex)
        {
            _viewModel.OutputLog += $"\n[PDF Preview] Error: {ex.Message}";
        }
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