using System.Collections.ObjectModel;

namespace DocumentManager.Models;

/// <summary>
/// Represents a node in the project tree view.
/// </summary>
public class ProjectTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ProjectTreeNodeType NodeType { get; set; }
    public bool HasNewerRevision { get; set; }
    public int RevisionNumber { get; set; }
    public int LatestRevision { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public ObservableCollection<ProjectTreeNode> Children { get; set; } = [];
}

public enum ProjectTreeNodeType
{
    Root,
    OuterFolder,
    SectionsFolder,
    Section,
    RevisionFolder,
    File,
    ImagesFolder,
    TemplatesFolder,
    SnippetsFolder,
    SharedSectionsFolder,
    CommonImagesFolder,
    ProjectImagesFolder
}
