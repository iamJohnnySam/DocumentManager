using System.IO;
using DocumentManager.Models;

namespace DocumentManager.Services;

/// <summary>
/// Provides search functionality across all project LaTeX files and revision notes.
/// </summary>
public class SearchService
{
    private readonly FileService _fileService;

    public SearchService(FileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Searches all .tex and .txt files in the project for the given query string.
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string projectRoot, string query)
    {
        var results = new List<SearchResult>();
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(projectRoot))
            return results;

        var searchExtensions = new[] { ".tex", ".txt" };
        var allFiles = Directory.GetFiles(projectRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => searchExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        foreach (var file in allFiles)
        {
            try
            {
                var content = await _fileService.ReadFileAsync(file);
                var lines = content.Split('\n');

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new SearchResult
                        {
                            FilePath = file,
                            FileName = Path.GetFileName(file),
                            LineNumber = i + 1,
                            LineContent = lines[i].Trim(),
                            MatchContext = GetContext(lines, i)
                        });
                    }
                }
            }
            catch
            {
                // Skip files that cannot be read
            }
        }

        return results;
    }

    private static string GetContext(string[] lines, int index)
    {
        var start = Math.Max(0, index - 1);
        var end = Math.Min(lines.Length - 1, index + 1);
        return string.Join("\n", lines[start..(end + 1)]).Trim();
    }
}
