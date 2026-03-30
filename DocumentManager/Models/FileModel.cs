namespace DocumentManager.Models;

/// <summary>
/// Represents a LaTeX file within the project.
/// </summary>
public class FileModel
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public bool HasNewerRevision { get; set; }
    public int LatestRevision { get; set; }
}
