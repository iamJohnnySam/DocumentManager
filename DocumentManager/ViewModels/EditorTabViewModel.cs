namespace DocumentManager.ViewModels;

/// <summary>
/// ViewModel for a single editor tab.
/// </summary>
public class EditorTabViewModel : ViewModelBase
{
    private string _content = string.Empty;
    private string _filePath = string.Empty;
    private string _title = string.Empty;
    private bool _isDirty;
    private bool _hasNewerRevision;
    private string _sectionName = string.Empty;
    private string _sectionPath = string.Empty;
    private int _revisionNumber;
    private string _lineNumbers = "1";

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value))
            {
                IsDirty = true;
                UpdateLineNumbers();
            }
        }
    }

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string DisplayTitle => IsDirty ? $"{_title} *" : _title;

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
                OnPropertyChanged(nameof(DisplayTitle));
        }
    }

    public bool HasNewerRevision
    {
        get => _hasNewerRevision;
        set => SetProperty(ref _hasNewerRevision, value);
    }

    public string SectionName
    {
        get => _sectionName;
        set => SetProperty(ref _sectionName, value);
    }

    public string SectionPath
    {
        get => _sectionPath;
        set => SetProperty(ref _sectionPath, value);
    }

    public int RevisionNumber
    {
        get => _revisionNumber;
        set => SetProperty(ref _revisionNumber, value);
    }

    public string LineNumbers
    {
        get => _lineNumbers;
        set => SetProperty(ref _lineNumbers, value);
    }

    private void UpdateLineNumbers()
    {
        var lineCount = string.IsNullOrEmpty(_content) ? 1 : _content.Split('\n').Length;
        LineNumbers = string.Join("\n", Enumerable.Range(1, lineCount));
    }

    /// <summary>
    /// Marks the content as loaded (not dirty) after initial population.
    /// </summary>
    public void MarkClean()
    {
        _isDirty = false;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DisplayTitle));
    }
}
