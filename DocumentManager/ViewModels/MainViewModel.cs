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

    // PDF Preview
    private string? _compiledPdfPath;
    public string? CompiledPdfPath
    {
        get => _compiledPdfPath;
        set
        {
            if (SetProperty(ref _compiledPdfPath, value))
                OnPropertyChanged(nameof(IsPdfPreviewVisible));
        }
    }

    public bool IsPdfPreviewVisible => !string.IsNullOrEmpty(CompiledPdfPath) && File.Exists(CompiledPdfPath);

    // Available projects discovered under the common root
    public ObservableCollection<string> AvailableProjectCodes { get; } = [];

    // Commands
    public ICommand NewProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAllCommand { get; }
    public ICommand CompileCommand { get; }
    public ICommand AddSectionCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand InsertSnippetCommand { get; }
    public ICommand InsertImageCommand { get; }
    public ICommand RefreshTreeCommand { get; }
    public ICommand CheckNotificationsCommand { get; }
    public ICommand SaveSnippetCommand { get; }
    public ICommand BrowseAvailableFilesCommand { get; }
    public ICommand ImportProjectImageCommand { get; }
    public ICommand ImportCommonImageCommand { get; }
    public ICommand SaveAsTemplateCommand { get; }
    public ICommand OpenGitHubCommand { get; }
    public ICommand ShowAboutCommand { get; }

    // UI dialog callbacks (set by the view)
    public Func<List<string>, (string ProjectCode, string Title, string Author, string TemplateName)?>? RequestNewProjectInfo { get; set; }
    public Func<string?>? RequestOpenProjectCode { get; set; }
    public Func<string?>? RequestSectionName { get; set; }
    public Func<string?>? RequestTemplateName { get; set; }
    public Func<(string Name, string Content)?>? RequestSnippetInfo { get; set; }
    public Action<string, string, string>? ShowDiffViewer { get; set; }
    public Action? ShowAboutDialog { get; set; }

    /// <summary>
    /// Callback to show the file browser window.
    /// Parameters: shared sections path, set of already-included names.
    /// Returns: list of (name, latestRevision) the user selected, or null if cancelled.
    /// </summary>
    public Func<string, IReadOnlySet<string>, List<(string Name, int LatestRevision)>?>? RequestBrowseAvailableFiles { get; set; }

    /// <summary>
    /// Callback to open a file-picker dialog for importing images.
    /// Returns the selected file path, or null if cancelled.
    /// </summary>
    public Func<string?>? RequestImageFilePath { get; set; }

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
        ProjectTree.FileOpenRequested += async (path, section, sectionPath, rev) =>
            await Editor.OpenFileAsync(path, section, sectionPath, rev);

        // Commands
        NewProjectCommand = new AsyncRelayCommand(CreateNewProjectAsync);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsProjectLoaded);
        SaveAllCommand = new AsyncRelayCommand(async () => await Editor.SaveAllAsync(), () => IsProjectLoaded);
        CompileCommand = new AsyncRelayCommand(CompileAsync, () => IsProjectLoaded);
        AddSectionCommand = new AsyncRelayCommand(AddSectionAsync, () => IsProjectLoaded);
        SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync, () => IsProjectLoaded);
        InsertSnippetCommand = new RelayCommand(InsertSelectedSnippet, () => IsProjectLoaded && SelectedSnippet is not null);
        InsertImageCommand = new RelayCommand(InsertSelectedImage, () => IsProjectLoaded && ImageGallery.SelectedImage is not null);
        RefreshTreeCommand = new RelayCommand(RefreshTree, () => IsProjectLoaded);
        CheckNotificationsCommand = new AsyncRelayCommand(CheckForNewerRevisionsAsync, () => IsProjectLoaded);
        SaveSnippetCommand = new AsyncRelayCommand(SaveSnippetAsync, () => IsProjectLoaded);
        BrowseAvailableFilesCommand = new AsyncRelayCommand(BrowseAvailableFilesAsync, () => IsProjectLoaded && Editor.SelectedTab is not null);
        ImportProjectImageCommand = new AsyncRelayCommand(ImportProjectImageAsync, () => IsProjectLoaded);
        ImportCommonImageCommand = new AsyncRelayCommand(ImportCommonImageAsync, () => IsProjectLoaded);
        SaveAsTemplateCommand = new AsyncRelayCommand(SaveAsTemplateAsync, () => IsProjectLoaded);
        OpenGitHubCommand = new RelayCommand(OpenGitHub);
        ShowAboutCommand = new RelayCommand(() => ShowAboutDialog?.Invoke());

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
        var templates = _templateService.GetAvailableOuterTemplates(_appSettings.CommonRootPath);
        var info = RequestNewProjectInfo?.Invoke(templates);
        if (info is null) return;

        var (projectCode, title, author, templateName) = info.Value;
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
                UpdatedDate = DateTime.UtcNow,
                TemplateName = templateName
            };
            await _fileService.SaveMetadataAsync(projectRoot, metadata);

            // Create main.tex from selected template or default
            string mainContent;
            if (!string.IsNullOrEmpty(templateName) && templateName != "Default")
            {
                mainContent = await _templateService.LoadOuterTemplateAsync(commonRoot, templateName);
                if (string.IsNullOrEmpty(mainContent))
                    mainContent = _templateService.GetDefaultMainTemplate(title, author);
            }
            else
            {
                mainContent = _templateService.GetDefaultMainTemplate(title, author);
            }
            var mainPath = Path.Combine(projectRoot, "outer", "main.tex");
            await _fileService.WriteFileAsync(mainPath, mainContent);

            // Create initial revision history with the first outer revision
            var project = BuildProjectModel(metadata, projectRoot, commonRoot);
            var outerRev = _revisionService.CreateOuterRevision(project, []);
            metadata.OuterRevisions.Add(outerRev);
            await _fileService.SaveMetadataAsync(projectRoot, metadata);
            await _revisionService.GenerateRevisionHistoryAsync(project, metadata.OuterRevisions);

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

            // Check for existing compiled PDF
            var existingPdf = Path.Combine(projectRoot, "outer", "main.pdf");
            CompiledPdfPath = File.Exists(existingPdf) ? existingPdf : null;

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

            // Create a new outer revision and regenerate the revision history table
            var metadata = await _fileService.LoadMetadataAsync(CurrentProject.RootPath);
            var outerRev = _revisionService.CreateOuterRevision(CurrentProject, metadata.OuterRevisions);
            metadata.OuterRevisions.Add(outerRev);
            CurrentProject.CurrentRevision = outerRev.RevisionNumber;
            await _revisionService.GenerateRevisionHistoryAsync(CurrentProject, metadata.OuterRevisions);

            var mainTexPath = Path.Combine(CurrentProject.RootPath, "outer", "main.tex");
            var outputDir = Path.Combine(CurrentProject.RootPath, "outer");
            var result = await _compilerService.CompileAsync(mainTexPath, SelectedCompiler, outputDir);

            if (result.Success)
            {
                AppendLog("Compilation successful!");
                StatusMessage = "Compilation successful.";

                // Update PDF preview path
                var pdfPath = Path.ChangeExtension(mainTexPath, ".pdf");
                if (File.Exists(pdfPath))
                    CompiledPdfPath = pdfPath;
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

    // ─── Save (with revision support) ─────────────────────────────────

    /// <summary>
    /// Saves the current tab. If the file is a section, prompts for revision
    /// notes, creates a new revision, updates \input references, and refreshes the tree.
    /// </summary>
    private async Task SaveAsync()
    {
        var revInfo = await Editor.SaveCurrentTabAsync();
        if (revInfo is not null)
        {
            var (sectionName, sectionPath, oldRev, newRev) = revInfo.Value;
            var isNewRevision = newRev > oldRev;

            if (isNewRevision)
            {
                // Update \input / \include references in open tabs and project files
                await UpdateSectionReferencesAsync(sectionName, oldRev, newRev);
            }

            // Update the in-memory project model
            if (CurrentProject is not null)
            {
                var section = CurrentProject.Sections.FirstOrDefault(s => s.Name == sectionName);
                if (section is not null)
                {
                    section.CurrentRevision = newRev;
                    section.Revisions = BuildRevisionList(section.SectionPath);
                }
                else
                {
                    CurrentProject.Sections.Add(new SectionModel
                    {
                        Name = sectionName,
                        SectionPath = sectionPath,
                        CurrentRevision = newRev,
                        Revisions = BuildRevisionList(sectionPath)
                    });
                }

                await SaveProjectMetadataAsync();
                ProjectTree.LoadProject(CurrentProject);
            }

            if (isNewRevision)
            {
                AppendLog($"Revision v{newRev:D3} created for '{sectionName}'. References updated.");
                StatusMessage = $"Revision v{newRev:D3} created for '{sectionName}'.";
            }
            else
            {
                AppendLog($"Section '{sectionName}' saved in-place (v{newRev:D3}).");
                StatusMessage = $"Section '{sectionName}' saved.";
            }
        }
    }

    /// <summary>
    /// Updates \input / \include path references from the old revision to the
    /// new revision in open editor tabs and on-disk project files.
    /// </summary>
    private async Task UpdateSectionReferencesAsync(string sectionName, int oldRev, int newRev)
    {
        var oldRef = $"{sectionName}/v{oldRev:D3}";
        var newRef = $"{sectionName}/v{newRev:D3}";

        // Update all open tabs
        foreach (var tab in Editor.OpenTabs)
        {
            if (tab.Content.Contains(oldRef))
            {
                tab.Content = tab.Content.Replace(oldRef, newRef);
                await _fileService.WriteFileAsync(tab.FilePath, tab.Content);
                tab.MarkClean();
            }
        }

        // Also update .tex files in the outer folder that aren't currently open
        if (CurrentProject is not null)
        {
            var outerPath = Path.Combine(CurrentProject.RootPath, "outer");
            if (Directory.Exists(outerPath))
            {
                foreach (var texFile in Directory.GetFiles(outerPath, "*.tex"))
                {
                    if (Editor.OpenTabs.Any(t =>
                        string.Equals(t.FilePath, texFile, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var content = await _fileService.ReadFileAsync(texFile);
                    if (content.Contains(oldRef))
                    {
                        content = content.Replace(oldRef, newRef);
                        await _fileService.WriteFileAsync(texFile, content);
                    }
                }
            }
        }
    }

    // ─── Sections (shared) ───────────────────────────────────────────

    /// <summary>
    /// Creates a new shared section in the common root, includes it in the
    /// current project, and inserts an \input reference in the active editor tab.
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
            var revNum = await _revisionService.CreateNewRevisionAsync(sectionPath, initialContent, $"Initial Release of section {sectionName}");

            CurrentProject.Sections.Add(new SectionModel
            {
                Name = sectionName,
                SectionPath = sectionPath,
                CurrentRevision = revNum,
                Revisions = BuildRevisionList(sectionPath)
            });

            // Insert \input reference in the current editor tab
            if (Editor.SelectedTab is not null)
            {
                var currentFileDir = Path.GetDirectoryName(Editor.SelectedTab.FilePath) ?? "";
                var contentPath = Path.Combine(sectionPath, $"v{revNum:D3}", "content");
                var relativePath = Path.GetRelativePath(currentFileDir, contentPath).Replace('\\', '/');
                Editor.InsertTextAtCursor($"\n\\input{{{relativePath}}}\n");

                // Flush to disk so tree scanner picks up the new reference
                await _fileService.WriteFileAsync(Editor.SelectedTab.FilePath, Editor.SelectedTab.Content);
            }

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

    // ─── Browse available shared files ───────────────────────────────

    /// <summary>
    /// Opens the file browser window that scans available shared sections and
    /// inserts \input references for selected sections at the cursor position
    /// in the currently active editor tab.
    /// </summary>
    private async Task BrowseAvailableFilesAsync()
    {
        if (CurrentProject is null || Editor.SelectedTab is null) return;

        var sharedSectionsPath = Path.Combine(_appSettings.CommonRootPath, "sections");

        var selected = RequestBrowseAvailableFiles?.Invoke(sharedSectionsPath, new HashSet<string>());
        if (selected is null || selected.Count == 0) return;

        // Build \input references relative to the active file and insert at cursor
        var currentFileDir = Path.GetDirectoryName(Editor.SelectedTab.FilePath) ?? "";
        var lines = new List<string>();
        foreach (var (name, latestRevision) in selected)
        {
            var contentPath = Path.Combine(sharedSectionsPath, name, $"v{latestRevision:D3}", "content");
            var relativePath = Path.GetRelativePath(currentFileDir, contentPath).Replace('\\', '/');
            lines.Add($"\\input{{{relativePath}}}");
        }

        Editor.InsertTextAtCursor("\n" + string.Join("\n", lines) + "\n");

        // Flush modified content to disk so the tree scanner picks up new \input references
        if (Editor.SelectedTab is not null)
            await _fileService.WriteFileAsync(Editor.SelectedTab.FilePath, Editor.SelectedTab.Content);

        ProjectTree.LoadProject(CurrentProject);
        AppendLog($"Inserted {selected.Count} section reference(s) at cursor.");
        StatusMessage = "Section references inserted.";
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
        Editor.InsertTextAtCursor(SelectedSnippet.Content);
    }

    private void InsertSelectedImage()
    {
        if (ImageGallery.SelectedImage is null) return;
        var code = ImageGallery.GetIncludeGraphicsCode(ImageGallery.SelectedImage);
        Editor.InsertTextAtCursor(code);
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

        // Load existing metadata to preserve outer revisions
        var existing = await _fileService.LoadMetadataAsync(CurrentProject.RootPath);

        var metadata = new ProjectMetadata
        {
            ProjectCode = CurrentProject.ProjectCode,
            Title = CurrentProject.Title,
            CurrentRevision = CurrentProject.CurrentRevision,
            Compiler = SelectedCompiler,
            CreatedDate = CurrentProject.CreatedDate,
            UpdatedDate = DateTime.UtcNow,
            TemplateName = existing.TemplateName,
            OuterRevisions = existing.OuterRevisions,
            IncludedSections = CurrentProject.Sections.Select(s => new IncludedSectionEntry
            {
                Name = s.Name,
                PinnedRevision = s.CurrentRevision
            }).ToList()
        };

        await _fileService.SaveMetadataAsync(CurrentProject.RootPath, metadata);
    }

    // ─── Image Import ─────────────────────────────────────────────────

    private async Task ImportProjectImageAsync()
    {
        if (CurrentProject is null) return;

        var sourcePath = RequestImageFilePath?.Invoke();
        if (string.IsNullOrEmpty(sourcePath)) return;

        var destDir = Path.Combine(CurrentProject.RootPath, "images");
        var destPath = Path.Combine(destDir, Path.GetFileName(sourcePath));

        await _fileService.CopyFileAsync(sourcePath, destPath);
        ImageGallery.LoadImages(CurrentProject.RootPath, _appSettings.CommonRootPath);
        AppendLog($"Imported project image: {Path.GetFileName(sourcePath)}");
        StatusMessage = $"Image imported to project folder.";
    }

    private async Task ImportCommonImageAsync()
    {
        if (CurrentProject is null) return;

        var sourcePath = RequestImageFilePath?.Invoke();
        if (string.IsNullOrEmpty(sourcePath)) return;

        var destDir = Path.Combine(_appSettings.CommonRootPath, "images");
        var destPath = Path.Combine(destDir, Path.GetFileName(sourcePath));

        await _fileService.CopyFileAsync(sourcePath, destPath);
        ImageGallery.LoadImages(CurrentProject.RootPath, _appSettings.CommonRootPath);
        AppendLog($"Imported common image: {Path.GetFileName(sourcePath)}");
        StatusMessage = $"Image imported to common folder.";
    }

    // ─── Templates ────────────────────────────────────────────────────

    private async Task SaveAsTemplateAsync()
    {
        if (CurrentProject is null) return;

        var name = RequestTemplateName?.Invoke();
        if (string.IsNullOrWhiteSpace(name)) return;

        var mainTexPath = Path.Combine(CurrentProject.RootPath, "outer", "main.tex");
        if (!File.Exists(mainTexPath))
        {
            AppendLog("No main.tex found to save as template.");
            return;
        }

        var content = await _fileService.ReadFileAsync(mainTexPath);
        await _templateService.SaveOuterTemplateAsync(_appSettings.CommonRootPath, name, content);
        AppendLog($"Template '{name}' saved.");
        StatusMessage = $"Template '{name}' saved.";
    }

    // ─── Help ─────────────────────────────────────────────────────────

    private void OpenGitHub()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/iamJohnnySam/DocumentManager",
            UseShellExecute = true
        });
    }

    private void AppendLog(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        OutputLog = string.IsNullOrEmpty(OutputLog) ? timestamped : $"{OutputLog}\n{timestamped}";
    }
}
