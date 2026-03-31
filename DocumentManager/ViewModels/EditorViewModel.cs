using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocumentManager.Services;

namespace DocumentManager.ViewModels;

/// <summary>
/// Manages open editor tabs and editing operations.
/// </summary>
public class EditorViewModel : ViewModelBase
{
    private readonly FileService _fileService;
    private readonly RevisionService _revisionService;

    public ObservableCollection<EditorTabViewModel> OpenTabs { get; } = [];

    private EditorTabViewModel? _selectedTab;
    public EditorTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, value);
    }

    public ICommand CloseTabCommand { get; }
    public ICommand SaveCommand { get; }

    /// <summary>
    /// Callback to prompt the user for revision notes when saving a section file.
    /// Param1: section name. Param2: existing notes (for editing in-place).
    /// Returns (Notes, IsNewRevision), or null if cancelled.
    /// </summary>
    public Func<string, string, (string Notes, bool IsNewRevision)?>? RequestRevisionNotes { get; set; }

    public EditorViewModel(FileService fileService, RevisionService revisionService)
    {
        _fileService = fileService;
        _revisionService = revisionService;
        CloseTabCommand = new RelayCommand(CloseTab);
        SaveCommand = new AsyncRelayCommand(SaveCurrentTabAsync, () => SelectedTab?.IsDirty == true);
    }

    /// <summary>
    /// Opens a file in a new or existing tab.
    /// </summary>
    public async Task OpenFileAsync(string filePath, string sectionName = "", string sectionPath = "", int revisionNumber = 0)
    {
        var existing = OpenTabs.FirstOrDefault(t => t.FilePath == filePath);
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }

        var content = File.Exists(filePath) ? await _fileService.ReadFileAsync(filePath) : string.Empty;
        var tab = new EditorTabViewModel
        {
            FilePath = filePath,
            Title = !string.IsNullOrEmpty(sectionName)
                ? $"{sectionName} (v{revisionNumber:D3})"
                : Path.GetFileName(filePath),
            Content = content,
            SectionName = sectionName,
            SectionPath = sectionPath,
            RevisionNumber = revisionNumber
        };
        tab.MarkClean();

        OpenTabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>
    /// Saves the currently active tab to disk.
    /// For section files, prompts with New Revision / Save In Place options.
    /// Returns (SectionName, SectionPath, OldRev, NewRev) — when OldRev == NewRev it was saved in-place.
    /// Returns null for non-section files or if cancelled.
    /// </summary>
    public async Task<(string SectionName, string SectionPath, int OldRev, int NewRev)?> SaveCurrentTabAsync()
    {
        if (SelectedTab is null || !SelectedTab.IsDirty) return null;

        // Section file: prompt for save mode
        if (!string.IsNullOrEmpty(SelectedTab.SectionPath))
        {
            var existingNotes = await _revisionService.ReadRevisionUserNotesAsync(
                SelectedTab.SectionPath, SelectedTab.RevisionNumber);

            var result = RequestRevisionNotes?.Invoke(SelectedTab.SectionName, existingNotes);
            if (result is null) return null;

            var (notes, isNewRevision) = result.Value;

            if (isNewRevision)
            {
                var oldRev = SelectedTab.RevisionNumber;
                var newRev = await _revisionService.CreateNewRevisionAsync(
                    SelectedTab.SectionPath, SelectedTab.Content, notes);
                var newPath = Path.Combine(
                    SelectedTab.SectionPath,
                    _revisionService.GetRevisionFolderName(newRev),
                    "content.tex");

                var revResult = (SelectedTab.SectionName, SelectedTab.SectionPath, oldRev, newRev);

                SelectedTab.FilePath = newPath;
                SelectedTab.RevisionNumber = newRev;
                SelectedTab.Title = $"{SelectedTab.SectionName} (v{newRev:D3})";
                SelectedTab.MarkClean();

                return revResult;
            }
            else
            {
                // Save in place — overwrite current revision content and notes
                await _revisionService.UpdateRevisionInPlaceAsync(
                    SelectedTab.SectionPath, SelectedTab.RevisionNumber, SelectedTab.Content, notes);
                SelectedTab.MarkClean();

                return (SelectedTab.SectionName, SelectedTab.SectionPath,
                        SelectedTab.RevisionNumber, SelectedTab.RevisionNumber);
            }
        }

        // Normal file: save in-place
        await _fileService.WriteFileAsync(SelectedTab.FilePath, SelectedTab.Content);
        SelectedTab.MarkClean();
        return null;
    }

    /// <summary>
    /// Saves all open dirty tabs in-place (no revision prompt).
    /// Used during compilation.
    /// </summary>
    public async Task SaveAllAsync()
    {
        foreach (var tab in OpenTabs.Where(t => t.IsDirty))
        {
            await _fileService.WriteFileAsync(tab.FilePath, tab.Content);
            tab.MarkClean();
        }
    }

    private void CloseTab(object? parameter)
    {
        if (parameter is EditorTabViewModel tab)
        {
            OpenTabs.Remove(tab);
            if (SelectedTab == tab)
                SelectedTab = OpenTabs.FirstOrDefault();
        }
    }

    /// <summary>
    /// Inserts text at the end of the current tab's content.
    /// </summary>
    public void InsertText(string text)
    {
        if (SelectedTab is null) return;
        SelectedTab.Content += text;
    }

    /// <summary>
    /// Callback set by the view to insert text at the actual cursor position
    /// in the active editor TextBox. Falls back to <see cref="InsertText"/> if unset.
    /// </summary>
    public Action<string>? InsertTextAtCursorCallback { get; set; }

    /// <summary>
    /// Inserts text at the cursor position when possible, otherwise appends.
    /// </summary>
    public void InsertTextAtCursor(string text)
    {
        if (InsertTextAtCursorCallback is not null)
        {
            InsertTextAtCursorCallback(text);
            return;
        }
        InsertText(text);
    }
}
