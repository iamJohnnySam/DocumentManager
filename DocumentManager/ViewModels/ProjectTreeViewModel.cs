using System.Collections.ObjectModel;
using System.IO;
using DocumentManager.Models;
using DocumentManager.Services;

namespace DocumentManager.ViewModels;

/// <summary>
/// Builds and manages the project tree view structure.
/// </summary>
public class ProjectTreeViewModel : ViewModelBase
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
    /// </summary>
    public event Action<string, string, int>? FileOpenRequested;

    public ProjectTreeViewModel(FileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Rebuilds the tree from the given project model.
    /// Shows the outer document, included shared sections, project images, common images, etc.
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

        // Included Sections (from shared folder)
        var sectionsNode = new ProjectTreeNode
        {
            Name = "Included Sections",
            FullPath = Path.Combine(project.CommonRootPath, "sections"),
            NodeType = ProjectTreeNodeType.SectionsFolder
        };

        foreach (var section in project.Sections)
        {
            var latestRev = _fileService.GetLatestRevisionNumber(section.SectionPath);
            var sectionNode = new ProjectTreeNode
            {
                Name = $"{section.Name}  [pinned: v{section.CurrentRevision:D3}]",
                FullPath = section.SectionPath,
                NodeType = ProjectTreeNodeType.Section,
                SectionName = section.Name,
                HasNewerRevision = latestRev > section.CurrentRevision,
                RevisionNumber = section.CurrentRevision,
                LatestRevision = latestRev
            };

            var revFolders = _fileService.GetRevisionFolders(section.SectionPath);
            foreach (var revFolder in revFolders)
            {
                var revPath = Path.Combine(section.SectionPath, revFolder);
                var revNum = int.TryParse(revFolder.AsSpan(1), out var n) ? n : 0;
                var revNode = new ProjectTreeNode
                {
                    Name = revFolder,
                    FullPath = revPath,
                    NodeType = ProjectTreeNodeType.RevisionFolder,
                    RevisionNumber = revNum,
                    LatestRevision = latestRev,
                    HasNewerRevision = revNum < latestRev,
                    SectionName = section.Name
                };

                var files = _fileService.GetFilesInDirectory(revPath, "*.tex");
                foreach (var file in files)
                {
                    revNode.Children.Add(new ProjectTreeNode
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        NodeType = ProjectTreeNodeType.File,
                        RevisionNumber = revNum,
                        LatestRevision = latestRev,
                        HasNewerRevision = revNum < latestRev,
                        SectionName = section.Name
                    });
                }

                sectionNode.Children.Add(revNode);
            }

            sectionsNode.Children.Add(sectionNode);
        }
        root.Children.Add(sectionsNode);

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
    /// Triggers the FileOpenRequested event for the given node.
    /// </summary>
    public void RequestOpenFile(ProjectTreeNode node)
    {
        if (node.NodeType == ProjectTreeNodeType.File)
        {
            FileOpenRequested?.Invoke(node.FullPath, node.SectionName, node.RevisionNumber);
        }
    }
}
