namespace DocumentManager.Models;

/// <summary>
/// Represents a reusable LaTeX code snippet.
/// </summary>
public class SnippetModel
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
