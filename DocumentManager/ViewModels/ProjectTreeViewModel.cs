using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using DocumentManager.Models;
using DocumentManager.Services;

namespace DocumentManager.ViewModels;

/// <summary>
/// Builds and manages the project tree view structure.
/// Automatically detects \input / \include references to build the tree.
/// </summary>
public partial class ProjectTreeViewModel : ViewModelBase
{
    private readonly FileService _fileService;

    public ObservableCollection<ProjectTreeNode> Nodes { get; } = [];

    private ProjectTreeNode? _selectedNode;
    public ProjectTreeNode? SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value);
    }

    /// <summary>
    /// Raised when a file node is double-clicked, requesting it to be opened in the editor.
    /// Parameters: filePath, sectionName, sectionPath, revisionNumber.
    /// </summary>
    public event Action<string, string, string, int>? FileOpenRequested;

    public ProjectTreeViewModel(FileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Rebuilds the tree from the given project model.
    /// Scans main.tex for \input / \include references to auto-detect referenced sections.
    /// </summary>
    public void LoadProject(ProjectModel project)
    {
        Nodes.Clear();

        var root = new ProjectTreeNode
        {
            Name = $"{project.Title} ({project.ProjectCode})",
            FullPath = project.RootPath,
            NodeType = ProjectTreeNodeType.Root
        };

        // Outer folder
        var outerPath = Path.Combine(project.RootPath, "outer");
        var outerNode = new ProjectTreeNode
        {
            Name = "Outer Document",
            FullPath = outerPath,
            NodeType = ProjectTreeNodeType.OuterFolder
        };

        foreach (var texFile in new[] { "main.tex", "revision_history.tex" })
        {
            var path = Path.Combine(outerPath, texFile);
            if (File.Exists(path))
            {
                outerNode.Children.Add(new ProjectTreeNode
                {
                    Name = texFile,
                    FullPath = path,
                    NodeType = ProjectTreeNodeType.File
                });
            }
        }
        root.Children.Add(outerNode);

        // Referenced Sections (auto-detected from \input / \include in main.tex)
        var sectionsRoot = Path.Combine(project.CommonRootPath, "sections");
        var referencedNode = new ProjectTreeNode
        {
            Name = "Referenced Sections",
            FullPath = sectionsRoot,
            NodeType = ProjectTreeNodeType.SectionsFolder
        };

        var mainTexPath = Path.Combine(outerPath, "main.tex");
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanIncludesForSections(referencedNode, mainTexPath, outerPath, sectionsRoot, visited);

        root.Children.Add(referencedNode);

        // Project Images (inside project folder)
        root.Children.Add(new ProjectTreeNode
        {
            Name = "Project Images",
            FullPath = Path.Combine(project.RootPath, "images"),
            NodeType = ProjectTreeNodeType.ProjectImagesFolder
        });

        // Common Images (shared, outside project)
        if (!string.IsNullOrEmpty(project.CommonRootPath))
        {
            root.Children.Add(new ProjectTreeNode
            {
                Name = "Common Images (shared)",
                FullPath = Path.Combine(project.CommonRootPath, "images"),
                NodeType = ProjectTreeNodeType.CommonImagesFolder
            });
        }

        // Templates folder
        root.Children.Add(new ProjectTreeNode
        {
            Name = "Templates",
            FullPath = Path.Combine(project.RootPath, "templates"),
            NodeType = ProjectTreeNodeType.TemplatesFolder
        });

        // Snippets folder
        root.Children.Add(new ProjectTreeNode
        {
            Name = "Snippets",
            FullPath = Path.Combine(project.RootPath, "snippets"),
            NodeType = ProjectTreeNodeType.SnippetsFolder
        });

        Nodes.Add(root);
    }

    /// <summary>
    /// Recursively scans a LaTeX file for \input / \include commands.
    /// Section files (under the sections root) are added as child nodes.
    /// Non-section includes are scanned transparently (their section children bubble up).
    /// </summary>
    private void ScanIncludesForSections(
        ProjectTreeNode parentNode, string filePath, string baseDir,
        string sectionsRoot, HashSet<string> visited)
    {
        if (!File.Exists(filePath) || !visited.Add(Path.GetFullPath(filePath)))
            return;

        string content;
        try { content = File.ReadAllText(filePath); }
        catch { return; }

        foreach (Match match in IncludeRegex().Matches(content))
        {
            var refPath = match.Groups[1].Value;
            var resolvedPath = ResolveTexPath(refPath, baseDir);
            if (resolvedPath is null) continue;

            var sectionInfo = ExtractSectionInfo(resolvedPath, sectionsRoot);
            if (sectionInfo is not null)
            {
                var (sectionName, sectionPath, revNum) = sectionInfo.Value;
                var latestRev = _fileService.GetLatestRevisionNumber(sectionPath);

                var sectionNode = new ProjectTreeNode
                {
                    Name = $"{sectionName} (v{revNum:D3})",
                    FullPath = resolvedPath,
                    NodeType = ProjectTreeNodeType.IncludedFile,
                    SectionName = sectionName,
                    SectionPath = sectionPath,
                    RevisionNumber = revNum,
                    LatestRevision = latestRev,
                    HasNewerRevision = latestRev > revNum
                };

                var childDir = Path.GetDirectoryName(resolvedPath) ?? baseDir;
                ScanIncludesForSections(sectionNode, resolvedPath, childDir, sectionsRoot, visited);
                parentNode.Children.Add(sectionNode);
            }
            else
            {
                // Non-section file: scan through it but add its section children to our parent
                var childDir = Path.GetDirectoryName(resolvedPath) ?? baseDir;
                ScanIncludesForSections(parentNode, resolvedPath, childDir, sectionsRoot, visited);
            }
        }
    }

    private static string? ResolveTexPath(string reference, string baseDir)
    {
        var path = Path.GetFullPath(Path.Combine(baseDir, reference));
        if (File.Exists(path)) return path;
        if (File.Exists(path + ".tex")) return path + ".tex";
        return null;
    }

    private static (string SectionName, string SectionPath, int RevNum)? ExtractSectionInfo(
        string filePath, string sectionsRoot)
    {
        var normalized = Path.GetFullPath(filePath);
        var normalizedRoot = Path.GetFullPath(sectionsRoot);
        if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar))
            normalizedRoot += Path.DirectorySeparatorChar;

        if (!normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return null;

        var relative = Path.GetRelativePath(normalizedRoot, normalized);
        var parts = relative.Split(Path.DirectorySeparatorChar);
        if (parts.Length < 2) return null;

        var sectionName = parts[0];
        var sectionPath = Path.Combine(sectionsRoot, sectionName);
        var revNum = 0;

        if (parts[1].StartsWith('v') && int.TryParse(parts[1].AsSpan(1), out var n))
            revNum = n;

        return (sectionName, sectionPath, revNum);
    }

    /// <summary>
    /// Triggers the FileOpenRequested event for the given node.
    /// </summary>
    public void RequestOpenFile(ProjectTreeNode node)
    {
        if (node.NodeType is ProjectTreeNodeType.File or ProjectTreeNodeType.IncludedFile)
        {
            FileOpenRequested?.Invoke(node.FullPath, node.SectionName, node.SectionPath, node.RevisionNumber);
        }
    }

    [GeneratedRegex(@"\\(?:input|include)\{([^}]+)\}")]
    private static partial Regex IncludeRegex();
}
