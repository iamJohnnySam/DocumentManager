namespace DocumentManager.Models;

/// <summary>
/// Represents a search result found across project files.
/// </summary>
public class SearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public string MatchContext { get; set; } = string.Empty;
}
