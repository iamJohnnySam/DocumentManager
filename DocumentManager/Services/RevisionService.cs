using System.IO;
using System.Text.RegularExpressions;
using DocumentManager.Models;

namespace DocumentManager.Services;

/// <summary>
/// Manages versioned revisions for sections and the outer document.
/// </summary>
public partial class RevisionService
{
    private readonly FileService _fileService;

    public RevisionService(FileService fileService)
    {
        _fileService = fileService;
    }

    public string GetRevisionFolderName(int revisionNumber) => $"v{revisionNumber:D3}";

    /// <summary>
    /// Creates a new revision folder with the given content and notes.
    /// </summary>
    public async Task<int> CreateNewRevisionAsync(string sectionPath, string content, string notes)
    {
        var latestRevision = _fileService.GetLatestRevisionNumber(sectionPath);
        var newRevision = latestRevision + 1;
        var folderName = GetRevisionFolderName(newRevision);
        var revisionPath = Path.Combine(sectionPath, folderName);
        Directory.CreateDirectory(revisionPath);

        var filePath = Path.Combine(revisionPath, "content.tex");
        await _fileService.WriteFileAsync(filePath, content);

        var notesPath = Path.Combine(revisionPath, "revision_notes.txt");
        await _fileService.WriteFileAsync(notesPath, $"Revision {newRevision} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}\n{notes}");

        return newRevision;
    }

    /// <summary>
    /// Reads the content of a specific revision.
    /// </summary>
    public async Task<string> GetRevisionContentAsync(string sectionPath, int revisionNumber)
    {
        var folderName = GetRevisionFolderName(revisionNumber);
        var filePath = Path.Combine(sectionPath, folderName, "content.tex");
        if (!File.Exists(filePath))
        {
            var dir = Path.Combine(sectionPath, folderName);
            if (Directory.Exists(dir))
            {
                var texFiles = Directory.GetFiles(dir, "*.tex");
                if (texFiles.Length > 0)
                    return await _fileService.ReadFileAsync(texFiles[0]);
            }
            return string.Empty;
        }
        return await _fileService.ReadFileAsync(filePath);
    }

    /// <summary>
    /// Generates the revision_history.tex file aggregating all included section revisions.
    /// Reads revision notes from the file system since sections are shared.
    /// </summary>
    public async Task<string> GenerateRevisionHistoryAsync(ProjectModel project)
    {
        var lines = new List<string>
        {
            "% Auto-generated revision history",
            $"% Project: {project.Title} ({project.ProjectCode})",
            $"% Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            "",
            "\\section*{Revision History}",
            "\\begin{tabular}{|l|l|l|p{8cm}|}",
            "\\hline",
            "\\textbf{Section} & \\textbf{Rev} & \\textbf{Date} & \\textbf{Notes} \\\\",
            "\\hline"
        };

        lines.Add($"Main Document & v{project.CurrentRevision:D3} & {project.UpdatedDate:yyyy-MM-dd} & Project revision \\\\");
        lines.Add("\\hline");

        foreach (var section in project.Sections)
        {
            // Read notes from each revision folder on disk
            var revFolders = _fileService.GetRevisionFolders(section.SectionPath);
            foreach (var revFolder in revFolders.OrderByDescending(f => f))
            {
                var revPath = Path.Combine(section.SectionPath, revFolder);
                var notesPath = Path.Combine(revPath, "revision_notes.txt");
                var notes = File.Exists(notesPath) ? await _fileService.ReadFileAsync(notesPath) : "";
                var revNum = int.TryParse(revFolder.AsSpan(1), out var n) ? n : 0;

                // Get folder creation date as a fallback
                var dateStr = Directory.GetCreationTimeUtc(revPath).ToString("yyyy-MM-dd");

                lines.Add($"{EscapeLatex(section.Name)} & v{revNum:D3} & {dateStr} & {EscapeLatex(notes.Split('\n')[0])} \\\\");
                lines.Add("\\hline");
            }
        }

        lines.Add("\\end{tabular}");
        lines.Add("");

        var content = string.Join("\n", lines);
        var historyPath = Path.Combine(project.RootPath, "outer", "revision_history.tex");
        await _fileService.WriteFileAsync(historyPath, content);

        return content;
    }

    /// <summary>
    /// Scans main.tex content for \input and \include references.
    /// </summary>
    public List<IncludedFile> ScanIncludes(string mainTexContent)
    {
        var results = new List<IncludedFile>();
        foreach (Match match in IncludePattern().Matches(mainTexContent))
        {
            results.Add(new IncludedFile
            {
                OriginalReference = match.Value,
                ReferencePath = match.Groups[1].Value
            });
        }
        return results;
    }

    /// <summary>
    /// Detects sections that have newer revisions than what is currently referenced.
    /// </summary>
    public List<(string SectionName, int CurrentRevision, int LatestRevision)> DetectNewerRevisions(ProjectModel project)
    {
        var results = new List<(string, int, int)>();
        foreach (var section in project.Sections)
        {
            var latest = _fileService.GetLatestRevisionNumber(section.SectionPath);
            if (latest > section.CurrentRevision)
            {
                results.Add((section.Name, section.CurrentRevision, latest));
            }
        }
        return results;
    }

    /// <summary>
    /// Computes a simple line-based diff between two text contents.
    /// </summary>
    public List<DiffLine> ComputeDiff(string oldContent, string newContent)
    {
        var oldLines = oldContent.Split('\n');
        var newLines = newContent.Split('\n');
        var result = new List<DiffLine>();

        var maxLines = Math.Max(oldLines.Length, newLines.Length);
        for (var i = 0; i < maxLines; i++)
        {
            var oldLine = i < oldLines.Length ? oldLines[i] : null;
            var newLine = i < newLines.Length ? newLines[i] : null;

            if (oldLine == newLine)
            {
                result.Add(new DiffLine { LineNumber = i + 1, OldText = oldLine ?? "", NewText = newLine ?? "", Type = DiffType.Unchanged });
            }
            else if (oldLine is null)
            {
                result.Add(new DiffLine { LineNumber = i + 1, OldText = "", NewText = newLine ?? "", Type = DiffType.Added });
            }
            else if (newLine is null)
            {
                result.Add(new DiffLine { LineNumber = i + 1, OldText = oldLine, NewText = "", Type = DiffType.Removed });
            }
            else
            {
                result.Add(new DiffLine { LineNumber = i + 1, OldText = oldLine, NewText = newLine, Type = DiffType.Modified });
            }
        }

        return result;
    }

    private static string EscapeLatex(string text)
    {
        return text
            .Replace("&", "\\&")
            .Replace("%", "\\%")
            .Replace("$", "\\$")
            .Replace("#", "\\#")
            .Replace("_", "\\_")
            .Replace("{", "\\{")
            .Replace("}", "\\}");
    }

    [GeneratedRegex(@"\\(?:input|include)\{([^}]+)\}")]
    private static partial Regex IncludePattern();
}

public class IncludedFile
{
    public string OriginalReference { get; set; } = string.Empty;
    public string ReferencePath { get; set; } = string.Empty;
}

public class DiffLine
{
    public int LineNumber { get; set; }
    public string OldText { get; set; } = string.Empty;
    public string NewText { get; set; } = string.Empty;
    public DiffType Type { get; set; }
}

public enum DiffType
{
    Unchanged,
    Added,
    Removed,
    Modified
}
