using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DocumentManager.Models;
using DocumentManager.Services;

namespace DocumentManager.ViewModels;

/// <summary>
/// Central ViewModel orchestrating all application functionality.
/// Sections and common images are stored in a shared common root folder.
/// Project-specific images are stored inside each project folder.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly FileService _fileService;
    private readonly RevisionService _revisionService;
    private readonly CompilerService _compilerService;
    private readonly TemplateService _templateService;
    private readonly SnippetService _snippetService;
    private readonly SearchService _searchService;
    private readonly SettingsService _settingsService;
    private AppSettings _appSettings;

    public EditorViewModel Editor { get; }
    public ProjectTreeViewModel ProjectTree { get; }
    public ImageGalleryViewModel ImageGallery { get; }

    /// <summary>The common root path shared across all projects.</summary>
    public string CommonRootPath => _appSettings.CommonRootPath;

    private ProjectModel? _currentProject;
    public ProjectModel? CurrentProject
    {
        get => _currentProject;
        set
        {
            if (SetProperty(ref _currentProject, value))
            {
                OnPropertyChanged(nameof(IsProjectLoaded));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public bool IsProjectLoaded => CurrentProject is not null;
    public string WindowTitle => CurrentProject is not null
        ? $"LaTeX Document Manager — {CurrentProject.Title} ({CurrentProject.ProjectCode})"
        : "LaTeX Document Manager";

    private string _outputLog = string.Empty;
    public string OutputLog
    {
        get => _outputLog;
        set => SetProperty(ref _outputLog, value);
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // Snippets
    public ObservableCollection<SnippetModel> Snippets { get; } = [];

    private SnippetModel? _selectedSnippet;
    public SnippetModel? SelectedSnippet
    {
        get => _selectedSnippet;
        set => SetProperty(ref _selectedSnippet, value);
    }

    // Search
    public ObservableCollection<SearchResult> SearchResults { get; } = [];

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    // Available compilers
    public ObservableCollection<string> AvailableCompilers { get; } = [];

    private string _selectedCompiler = "pdflatex";
    public string SelectedCompiler
    {
        get => _selectedCompiler;
        set => SetProperty(ref _selectedCompiler, value);
    }

    // Notifications
    public ObservableCollection<string> Notifications { get; } = [];

    // Available projects discovered under the common root
    public ObservableCollection<string> AvailableProjectCodes { get; } = [];

    // Commands
    public ICommand NewProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAllCommand { get; }
    public ICommand CompileCommand { get; }
    public ICommand AddSectionCommand { get; }
    public ICommand NewRevisionCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand InsertSnippetCommand { get; }
    public ICommand InsertImageCommand { get; }
    public ICommand RefreshTreeCommand { get; }
    public ICommand CheckNotificationsCommand { get; }
    public ICommand SaveSnippetCommand { get; }
    public ICommand BrowseAvailableFilesCommand { get; }

    // UI dialog callbacks (set by the view)
    public Func<(string ProjectCode, string Title, string Author)?>? RequestNewProjectInfo { get; set; }
    public Func<string?>? RequestOpenProjectCode { get; set; }
    public Func<string?>? RequestRevisionNotes { get; set; }
    public Func<string?>? RequestSectionName { get; set; }
    public Func<(string Name, string Content)?>? RequestSnippetInfo { get; set; }
    public Action<string, string, string>? ShowDiffViewer { get; set; }

    /// <summary>
    /// Callback to show the file browser window.
    /// Parameters: shared sections path, set of already-included names.
    /// Returns: list of (name, latestRevision) the user selected, or null if cancelled.
    /// </summary>
    public Func<string, IReadOnlySet<string>, List<(string Name, int LatestRevision)>?>? RequestBrowseAvailableFiles { get; set; }

    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _appSettings = _settingsService.Load();

        _fileService = new FileService();
        _revisionService = new RevisionService(_fileService);
        _compilerService = new CompilerService();
        _templateService = new TemplateService(_fileService);
        _snippetService = new SnippetService(_fileService);
        _searchService = new SearchService(_fileService);

        Editor = new EditorViewModel(_fileService, _revisionService);
        ProjectTree = new ProjectTreeViewModel(_fileService);
        ImageGallery = new ImageGalleryViewModel(_fileService);

        // Wire up tree file-open requests
        ProjectTree.FileOpenRequested += async (path, section, rev) =>
            await Editor.OpenFileAsync(path, section, rev);

        // Commands
        NewProjectCommand = new AsyncRelayCommand(CreateNewProjectAsync);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync);
        SaveCommand = new AsyncRelayCommand(async () => await Editor.SaveCurrentTabAsync(), () => IsProjectLoaded);
        SaveAllCommand = new AsyncRelayCommand(async () => await Editor.SaveAllAsync(), () => IsProjectLoaded);
        CompileCommand = new AsyncRelayCommand(CompileAsync, () => IsProjectLoaded);
        AddSectionCommand = new AsyncRelayCommand(AddSectionAsync, () => IsProjectLoaded);
        NewRevisionCommand = new AsyncRelayCommand(CreateNewRevisionAsync, () => IsProjectLoaded && Editor.SelectedTab is not null);
        SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync, () => IsProjectLoaded);
        InsertSnippetCommand = new RelayCommand(InsertSelectedSnippet, () => IsProjectLoaded && SelectedSnippet is not null);
        InsertImageCommand = new RelayCommand(InsertSelectedImage, () => IsProjectLoaded && ImageGallery.SelectedImage is not null);
        RefreshTreeCommand = new RelayCommand(RefreshTree, () => IsProjectLoaded);
        CheckNotificationsCommand = new AsyncRelayCommand(CheckForNewerRevisionsAsync, () => IsProjectLoaded);
        SaveSnippetCommand = new AsyncRelayCommand(SaveSnippetAsync, () => IsProjectLoaded);
        BrowseAvailableFilesCommand = new AsyncRelayCommand(BrowseAvailableFilesAsync, () => IsProjectLoaded);

        // Detect compilers
        DetectCompilers();
    }

    /// <summary>
    /// Initialises the common root path. Called by the view after SetupDialog completes.
    /// </summary>
    public void SetCommonRoot(string commonRootPath)
    {
        _appSettings.CommonRootPath = commonRootPath;
        _settingsService.Save(_appSettings);
        _fileService.EnsureCommonRootStructure(commonRootPath);
        RefreshAvailableProjects();
    }

    /// <summary>
    /// Scans the projects folder under the common root for existing project codes.
    /// </summary>
    public void RefreshAvailableProjects()
    {
        AvailableProjectCodes.Clear();
        var projectsDir = Path.Combine(_appSettings.CommonRootPath, "projects");
        if (!Directory.Exists(projectsDir)) return;

        foreach (var dir in Directory.GetDirectories(projectsDir))
        {
            var metadataPath = Path.Combine(dir, "metadata.json");
            if (File.Exists(metadataPath))
                AvailableProjectCodes.Add(Path.GetFileName(dir) ?? "");
        }
    }

    private void DetectCompilers()
    {
        AvailableCompilers.Clear();
        var compilers = _compilerService.DetectInstalledCompilers();
        foreach (var c in compilers)
            AvailableCompilers.Add(c);

        foreach (var c in new[] { "pdflatex", "xelatex", "lualatex" })
        {
            if (!AvailableCompilers.Contains(c))
                AvailableCompilers.Add(c);
        }

        if (AvailableCompilers.Count > 0)
            SelectedCompiler = AvailableCompilers[0];
    }

    // ─── Project creation / loading ──────────────────────────────────

    /// <summary>
    /// Creates a new project inside the common root.
    /// </summary>
    private async Task CreateNewProjectAsync()
    {
        var info = RequestNewProjectInfo?.Invoke();
        if (info is null) return;

        var (projectCode, title, author) = info.Value;
        var commonRoot = _appSettings.CommonRootPath;

        try
        {
            StatusMessage = "Creating project...";
            _fileService.CreateProjectStructure(commonRoot, projectCode);

            var projectRoot = Path.Combine(commonRoot, "projects", projectCode);

            var metadata = new ProjectMetadata
            {
                ProjectCode = projectCode,
                Title = title,
                CurrentRevision = 1,
                Compiler = SelectedCompiler,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            await _fileService.SaveMetadataAsync(projectRoot, metadata);

            // Create main.tex from template
            var mainContent = _templateService.GetDefaultMainTemplate(title, author);
            var mainPath = Path.Combine(projectRoot, "outer", "main.tex");
            await _fileService.WriteFileAsync(mainPath, mainContent);

            // Create initial revision history
            var project = BuildProjectModel(metadata, projectRoot, commonRoot);
            await _revisionService.GenerateRevisionHistoryAsync(project);

            await LoadProjectFromCodeAsync(projectCode);
            AppendLog($"Project '{title}' created at {projectRoot}");
            StatusMessage = "Project created.";
        }
        catch (Exception ex)
        {
            AppendLog($"Error creating project: {ex.Message}");
            StatusMessage = "Error creating project.";
        }
    }

    /// <summary>
    /// Opens an existing project by selecting its code from the available list.
    /// </summary>
    private async Task OpenProjectAsync()
    {
        var code = RequestOpenProjectCode?.Invoke();
        if (string.IsNullOrEmpty(code)) return;

        await LoadProjectFromCodeAsync(code);
    }

    /// <summary>
    /// Loads a project by its code from the common root's projects folder.
    /// </summary>
    public async Task LoadProjectFromCodeAsync(string projectCode)
    {
        var commonRoot = _appSettings.CommonRootPath;
        var projectRoot = Path.Combine(commonRoot, "projects", projectCode);
        await LoadProjectFromPathAsync(projectRoot);
    }

    /// <summary>
    /// Loads a project from an absolute path.
    /// </summary>
    public async Task LoadProjectFromPathAsync(string projectRoot)
    {
        var commonRoot = _appSettings.CommonRootPath;
        try
        {
            StatusMessage = "Loading project...";
            var metadata = await _fileService.LoadMetadataAsync(projectRoot);

            var project = BuildProjectModel(metadata, projectRoot, commonRoot);
            CurrentProject = project;

            ProjectTree.LoadProject(project);
            ImageGallery.LoadImages(projectRoot, commonRoot);
            await LoadSnippetsAsync();

            if (!string.IsNullOrEmpty(metadata.Compiler))
                SelectedCompiler = metadata.Compiler;

            // Auto-open main.tex
            var mainTexPath = Path.Combine(projectRoot, "outer", "main.tex");
            if (File.Exists(mainTexPath))
                await Editor.OpenFileAsync(mainTexPath);

            // Persist last opened project
            _appSettings.LastProjectCode = metadata.ProjectCode;
            _settingsService.Save(_appSettings);
            RefreshAvailableProjects();

            await CheckForNewerRevisionsAsync();

            AppendLog($"Project '{project.Title}' loaded from {projectRoot}");
            StatusMessage = "Project loaded.";
        }
        catch (Exception ex)
        {
            AppendLog($"Error loading project: {ex.Message}");
            StatusMessage = "Error loading project.";
        }
    }

    /// <summary>
    /// Builds the in-memory ProjectModel from metadata.
    /// Sections are resolved from the shared sections folder in the common root.
    /// </summary>
    private ProjectModel BuildProjectModel(ProjectMetadata metadata, string projectRoot, string commonRoot)
    {
        var project = new ProjectModel
        {
            ProjectCode = metadata.ProjectCode,
            Title = metadata.Title,
            RootPath = projectRoot,
            CommonRootPath = commonRoot,
            CurrentRevision = metadata.CurrentRevision,
            Compiler = metadata.Compiler,
            CreatedDate = metadata.CreatedDate,
            UpdatedDate = metadata.UpdatedDate
        };

        var sharedSectionsRoot = Path.Combine(commonRoot, "sections");

        foreach (var inc in metadata.IncludedSections)
        {
            var sectionPath = Path.Combine(sharedSectionsRoot, inc.Name);
            if (!Directory.Exists(sectionPath)) continue;

            var latestRev = _fileService.GetLatestRevisionNumber(sectionPath);
            project.Sections.Add(new SectionModel
            {
                Name = inc.Name,
                CurrentRevision = inc.PinnedRevision,
                SectionPath = sectionPath,
                Revisions = BuildRevisionList(sectionPath)
            });
        }

        return project;
    }

    private List<RevisionModel> BuildRevisionList(string sectionPath)
    {
        var list = new List<RevisionModel>();
        foreach (var folder in _fileService.GetRevisionFolders(sectionPath))
        {
            var revNum = int.TryParse(folder.AsSpan(1), out var n) ? n : 0;
            list.Add(new RevisionModel
            {
                RevisionNumber = revNum,
                RevisionDate = Directory.GetCreationTimeUtc(Path.Combine(sectionPath, folder)),
                FolderPath = Path.Combine(sectionPath, folder)
            });
        }
        return list;
    }

    // ─── Compilation ─────────────────────────────────────────────────

    private async Task CompileAsync()
    {
        if (CurrentProject is null) return;

        try
        {
            StatusMessage = "Compiling...";
            AppendLog($"Starting compilation with {SelectedCompiler}...");

            await Editor.SaveAllAsync();
            await _revisionService.GenerateRevisionHistoryAsync(CurrentProject);

            var mainTexPath = Path.Combine(CurrentProject.RootPath, "outer", "main.tex");
            var outputDir = Path.Combine(CurrentProject.RootPath, "outer");
            var result = await _compilerService.CompileAsync(mainTexPath, SelectedCompiler, outputDir);

            if (result.Success)
            {
                AppendLog("Compilation successful!");
                StatusMessage = "Compilation successful.";
            }
            else
            {
                AppendLog($"Compilation failed (exit code {result.ExitCode}):");
                StatusMessage = "Compilation failed.";
            }

            if (!string.IsNullOrEmpty(result.StandardOutput))
                AppendLog(result.StandardOutput);
            if (!string.IsNullOrEmpty(result.ErrorOutput))
                AppendLog($"ERRORS:\n{result.ErrorOutput}");

            await SaveProjectMetadataAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"Compilation error: {ex.Message}");
            StatusMessage = "Compilation error.";
        }
    }

    // ─── Sections (shared) ───────────────────────────────────────────

    /// <summary>
    /// Creates a new shared section in the common root and includes it in the current project.
    /// </summary>
    private async Task AddSectionAsync()
    {
        if (CurrentProject is null) return;

        var sectionName = RequestSectionName?.Invoke();
        if (string.IsNullOrWhiteSpace(sectionName)) return;

        try
        {
            var sectionPath = Path.Combine(_appSettings.CommonRootPath, "sections", sectionName);
            Directory.CreateDirectory(sectionPath);

            var initialContent = $"% Section: {sectionName}\n% Created: {DateTime.UtcNow:yyyy-MM-dd}\n\n\\section{{{sectionName}}}\n\n";
            var revNum = await _revisionService.CreateNewRevisionAsync(sectionPath, initialContent, "Initial creation");

            CurrentProject.Sections.Add(new SectionModel
            {
                Name = sectionName,
                SectionPath = sectionPath,
                CurrentRevision = revNum,
                Revisions = BuildRevisionList(sectionPath)
            });

            await SaveProjectMetadataAsync();
            ProjectTree.LoadProject(CurrentProject);

            AppendLog($"Shared section '{sectionName}' created and included.");
            StatusMessage = $"Section '{sectionName}' added.";
        }
        catch (Exception ex)
        {
            AppendLog($"Error adding section: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new revision of the current file's shared section.
    /// </summary>
    private async Task CreateNewRevisionAsync()
    {
        if (CurrentProject is null || Editor.SelectedTab is null) return;

        var sectionName = Editor.SelectedTab.SectionName;
        if (string.IsNullOrEmpty(sectionName))
        {
            AppendLog("Cannot create revision: file is not part of a section.");
            return;
        }

        var notes = RequestRevisionNotes?.Invoke();
        if (notes is null) return;

        var section = CurrentProject.Sections.FirstOrDefault(s => s.Name == sectionName);
        if (section is null) return;

        try
        {
            var newRev = await Editor.SaveAsNewRevisionAsync(section.SectionPath, notes);

            section.CurrentRevision = newRev;
            section.Revisions = BuildRevisionList(section.SectionPath);

            await SaveProjectMetadataAsync();
            ProjectTree.LoadProject(CurrentProject);

            AppendLog($"New revision v{newRev:D3} created for shared section '{sectionName}'.");
            StatusMessage = $"Revision v{newRev:D3} created.";
        }
        catch (Exception ex)
        {
            AppendLog($"Error creating revision: {ex.Message}");
        }
    }

    // ─── Browse available shared files ───────────────────────────────

    /// <summary>
    /// Opens the file browser window that scans available shared sections and lets
    /// the user pick which to include/exclude from the current project.
    /// </summary>
    private async Task BrowseAvailableFilesAsync()
    {
        if (CurrentProject is null) return;

        var sharedSectionsPath = Path.Combine(_appSettings.CommonRootPath, "sections");
        var alreadyIncluded = new HashSet<string>(CurrentProject.Sections.Select(s => s.Name));

        var selected = RequestBrowseAvailableFiles?.Invoke(sharedSectionsPath, alreadyIncluded);
        if (selected is null) return;

        // Rebuild the included sections list
        CurrentProject.Sections.Clear();
        foreach (var (name, latestRevision) in selected)
        {
            var sectionPath = Path.Combine(sharedSectionsPath, name);
            CurrentProject.Sections.Add(new SectionModel
            {
                Name = name,
                SectionPath = sectionPath,
                CurrentRevision = latestRevision,
                Revisions = BuildRevisionList(sectionPath)
            });
        }

        await SaveProjectMetadataAsync();
        ProjectTree.LoadProject(CurrentProject);

        AppendLog($"Included sections updated: {selected.Count} section(s).");
        StatusMessage = "Included sections updated.";
    }

    // ─── Search ──────────────────────────────────────────────────────

    private async Task ExecuteSearchAsync()
    {
        if (CurrentProject is null || string.IsNullOrWhiteSpace(SearchQuery)) return;

        try
        {
            StatusMessage = "Searching...";
            SearchResults.Clear();

            // Search project files
            var results = await _searchService.SearchAsync(CurrentProject.RootPath, SearchQuery);

            // Also search shared sections
            var sharedResults = await _searchService.SearchAsync(
                Path.Combine(_appSettings.CommonRootPath, "sections"), SearchQuery);
            results.AddRange(sharedResults);

            foreach (var r in results)
                SearchResults.Add(r);

            AppendLog($"Search for '{SearchQuery}': {results.Count} result(s).");
            StatusMessage = $"Search complete: {results.Count} result(s).";
        }
        catch (Exception ex)
        {
            AppendLog($"Search error: {ex.Message}");
        }
    }

    // ─── Notifications ───────────────────────────────────────────────

    private async Task CheckForNewerRevisionsAsync()
    {
        if (CurrentProject is null) return;

        Notifications.Clear();
        var newer = _revisionService.DetectNewerRevisions(CurrentProject);
        foreach (var (sectionName, currentRev, latestRev) in newer)
        {
            Notifications.Add($"'{sectionName}': v{currentRev:D3} → v{latestRev:D3} available");
        }

        if (Notifications.Count > 0)
            AppendLog($"{Notifications.Count} section(s) have newer revisions available.");

        await Task.CompletedTask;
    }

    // ─── Snippets / Images ───────────────────────────────────────────

    private void InsertSelectedSnippet()
    {
        if (SelectedSnippet is null) return;
        Editor.InsertText(SelectedSnippet.Content);
    }

    private void InsertSelectedImage()
    {
        if (ImageGallery.SelectedImage is null) return;
        var code = ImageGallery.GetIncludeGraphicsCode(ImageGallery.SelectedImage);
        Editor.InsertText(code);
    }

    private void RefreshTree()
    {
        if (CurrentProject is null) return;
        ProjectTree.LoadProject(CurrentProject);
        ImageGallery.LoadImages(CurrentProject.RootPath, _appSettings.CommonRootPath);
    }

    private async Task LoadSnippetsAsync()
    {
        if (CurrentProject is null) return;

        Snippets.Clear();
        foreach (var s in _snippetService.GetBuiltInSnippets())
            Snippets.Add(s);

        var projectSnippets = await _snippetService.LoadSnippetsAsync(CurrentProject.RootPath);
        foreach (var s in projectSnippets)
            Snippets.Add(s);
    }

    private async Task SaveSnippetAsync()
    {
        if (CurrentProject is null) return;

        var info = RequestSnippetInfo?.Invoke();
        if (info is null) return;

        var (name, content) = info.Value;
        await _snippetService.SaveSnippetAsync(CurrentProject.RootPath, name, content);
        await LoadSnippetsAsync();
        AppendLog($"Snippet '{name}' saved.");
    }

    // ─── Metadata persistence ────────────────────────────────────────

    private async Task SaveProjectMetadataAsync()
    {
        if (CurrentProject is null) return;

        var metadata = new ProjectMetadata
        {
            ProjectCode = CurrentProject.ProjectCode,
            Title = CurrentProject.Title,
            CurrentRevision = CurrentProject.CurrentRevision,
            Compiler = SelectedCompiler,
            CreatedDate = CurrentProject.CreatedDate,
            UpdatedDate = DateTime.UtcNow,
            IncludedSections = CurrentProject.Sections.Select(s => new IncludedSectionEntry
            {
                Name = s.Name,
                PinnedRevision = s.CurrentRevision
            }).ToList()
        };

        await _fileService.SaveMetadataAsync(CurrentProject.RootPath, metadata);
    }

    private void AppendLog(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        OutputLog = string.IsNullOrEmpty(OutputLog) ? timestamped : $"{OutputLog}\n{timestamped}";
    }
}
