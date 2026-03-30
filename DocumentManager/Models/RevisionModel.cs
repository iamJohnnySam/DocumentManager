namespace DocumentManager.Models;

/// <summary>
/// Represents a single revision of a section file.
/// </summary>
public class RevisionModel
{
    public int RevisionNumber { get; set; }
    public DateTime RevisionDate { get; set; }
    public string RevisionNotes { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
}
