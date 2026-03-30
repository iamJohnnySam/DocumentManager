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
    public async Task OpenFileAsync(string filePath, string sectionName = "", int revisionNumber = 0)
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
            Title = Path.GetFileName(filePath),
            Content = content,
            SectionName = sectionName,
            RevisionNumber = revisionNumber
        };
        tab.MarkClean();

        OpenTabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>
    /// Saves the currently active tab to disk.
    /// </summary>
    public async Task SaveCurrentTabAsync()
    {
        if (SelectedTab is null || !SelectedTab.IsDirty) return;
        await _fileService.WriteFileAsync(SelectedTab.FilePath, SelectedTab.Content);
        SelectedTab.MarkClean();
    }

    /// <summary>
    /// Saves the current tab content as a new revision.
    /// </summary>
    public async Task<int> SaveAsNewRevisionAsync(string sectionPath, string notes)
    {
        if (SelectedTab is null) return 0;

        var newRev = await _revisionService.CreateNewRevisionAsync(sectionPath, SelectedTab.Content, notes);
        var newPath = Path.Combine(sectionPath, _revisionService.GetRevisionFolderName(newRev), "content.tex");

        SelectedTab.FilePath = newPath;
        SelectedTab.RevisionNumber = newRev;
        SelectedTab.Title = $"content.tex (v{newRev:D3})";
        SelectedTab.MarkClean();

        return newRev;
    }

    /// <summary>
    /// Saves all open dirty tabs.
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
}
