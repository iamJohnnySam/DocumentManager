using System.Text.Json.Serialization;

namespace DocumentManager.Models;

/// <summary>
/// Serializable project metadata stored in metadata.json.
/// </summary>
public class ProjectMetadata
{
    [JsonPropertyName("projectCode")]
    public string ProjectCode { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("currentRevision")]
    public int CurrentRevision { get; set; } = 1;

    [JsonPropertyName("compiler")]
    public string Compiler { get; set; } = "pdflatex";

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedDate")]
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Shared sections included in this project, each pinned to a specific revision.
    /// Sections live in the common root's sections folder, not inside the project.
    /// </summary>
    [JsonPropertyName("includedSections")]
    public List<IncludedSectionEntry> IncludedSections { get; set; } = [];

    [JsonPropertyName("snippets")]
    public List<SnippetMetadataEntry> Snippets { get; set; } = [];
}

/// <summary>
/// Reference from a project to a shared section at a specific revision.
/// </summary>
public class IncludedSectionEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("pinnedRevision")]
    public int PinnedRevision { get; set; } = 1;
}

public class SectionMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("currentRevision")]
    public int CurrentRevision { get; set; } = 1;

    [JsonPropertyName("revisions")]
    public List<RevisionMetadataEntry> Revisions { get; set; } = [];
}

public class RevisionMetadataEntry
{
    [JsonPropertyName("revisionNumber")]
    public int RevisionNumber { get; set; }

    [JsonPropertyName("revisionDate")]
    public DateTime RevisionDate { get; set; }

    [JsonPropertyName("revisionNotes")]
    public string RevisionNotes { get; set; } = string.Empty;
}

public class SnippetMetadataEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
}
