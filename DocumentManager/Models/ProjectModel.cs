namespace DocumentManager.Models;

/// <summary>
/// In-memory representation of a loaded project.
/// </summary>
public class ProjectModel
{
    public string ProjectCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string CommonRootPath { get; set; } = string.Empty;
    public int CurrentRevision { get; set; } = 1;
    public string Compiler { get; set; } = "pdflatex";
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    /// <summary>
    /// Shared sections that this project includes (resolved from the common root).
    /// </summary>
    public List<SectionModel> Sections { get; set; } = [];
}

/// <summary>
/// Represents a shared section containing versioned LaTeX files.
/// The SectionPath points to the shared folder in the common root.
/// </summary>
public class SectionModel
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The revision this project has pinned / is currently using.
    /// </summary>
    public int CurrentRevision { get; set; } = 1;

    public List<RevisionModel> Revisions { get; set; } = [];

    /// <summary>
    /// Full path to the section folder inside the shared sections directory.
    /// </summary>
    public string SectionPath { get; set; } = string.Empty;
}
