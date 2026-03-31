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
    /// Saves content and notes in-place for an existing revision (no new version folder).
    /// Preserves the original timestamp in the notes header line.
    /// </summary>
    public async Task UpdateRevisionInPlaceAsync(string sectionPath, int revisionNumber, string content, string notes)
    {
        var folderName = GetRevisionFolderName(revisionNumber);
        var revisionPath = Path.Combine(sectionPath, folderName);

        var filePath = Path.Combine(revisionPath, "content.tex");
        await _fileService.WriteFileAsync(filePath, content);

        var notesPath = Path.Combine(revisionPath, "revision_notes.txt");
        var headerLine = $"Revision {revisionNumber} - {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
        if (File.Exists(notesPath))
        {
            var existing = await _fileService.ReadFileAsync(notesPath);
            var firstLine = existing.Split('\n').FirstOrDefault();
            if (!string.IsNullOrEmpty(firstLine) && firstLine.StartsWith("Revision"))
                headerLine = firstLine;
        }

        await _fileService.WriteFileAsync(notesPath, $"{headerLine}\n{notes}");
    }

    /// <summary>
    /// Reads the user-entered notes (lines after the header) from a revision's notes file.
    /// </summary>
    public async Task<string> ReadRevisionUserNotesAsync(string sectionPath, int revisionNumber)
    {
        var notesPath = Path.Combine(sectionPath, GetRevisionFolderName(revisionNumber), "revision_notes.txt");
        if (!File.Exists(notesPath)) return string.Empty;

        var content = await _fileService.ReadFileAsync(notesPath);
        var lines = content.Split('\n');
        return lines.Length > 1 ? string.Join("\n", lines.Skip(1)).Trim() : string.Empty;
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
    /// Generates the revision_history.tex file from the outer document revision entries.
    /// Each row shows the publish date, outer revision number, and aggregated notes.
    /// </summary>
    public async Task<string> GenerateRevisionHistoryAsync(ProjectModel project, List<OuterRevisionEntry> outerRevisions)
    {
        var lines = new List<string>
        {
            "% Auto-generated revision history",
            $"% Project: {project.Title} ({project.ProjectCode})",
            $"% Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            "",
            "\\section*{Revision History}",
            "\\begin{tabular}{|l|l|p{10cm}|}",
            "\\hline",
            "\\textbf{Date} & \\textbf{Rev} & \\textbf{Notes} \\\\",
            "\\hline"
        };

        foreach (var rev in outerRevisions.OrderBy(r => r.RevisionNumber))
        {
            lines.Add($"{rev.PublishDate:yyyy-MM-dd} & v{rev.RevisionNumber:D3} & {EscapeLatex(rev.Notes)} \\\\");
            lines.Add("\\hline");
        }

        lines.Add("\\end{tabular}");
        lines.Add("");

        var content = string.Join("\n", lines);
        var historyPath = Path.Combine(project.RootPath, "outer", "revision_history.tex");
        await _fileService.WriteFileAsync(historyPath, content);

        return content;
    }

    /// <summary>
    /// Creates a new outer document revision entry by aggregating section revision notes
    /// created since the last outer revision. The first outer revision is "Initial Release".
    /// </summary>
    public OuterRevisionEntry CreateOuterRevision(ProjectModel project, List<OuterRevisionEntry> existingRevisions)
    {
        var lastDate = existingRevisions.Count > 0
            ? existingRevisions.Max(r => r.PublishDate)
            : DateTime.MinValue;

        var newRevNum = existingRevisions.Count > 0
            ? existingRevisions.Max(r => r.RevisionNumber) + 1
            : 1;

        string notes;
        if (newRevNum == 1)
        {
            notes = "Initial Release";
        }
        else
        {
            var aggregated = AggregateSectionNotesSince(project, lastDate);
            notes = string.IsNullOrWhiteSpace(aggregated) ? "Document update" : aggregated;
        }

        return new OuterRevisionEntry
        {
            RevisionNumber = newRevNum,
            PublishDate = DateTime.UtcNow,
            Notes = notes
        };
    }

    /// <summary>
    /// Scans section revision folders for revisions created after the given date,
    /// aggregating their user-entered notes.
    /// </summary>
    private string AggregateSectionNotesSince(ProjectModel project, DateTime since)
    {
        var parts = new List<string>();

        foreach (var section in project.Sections)
        {
            if (!Directory.Exists(section.SectionPath)) continue;

            foreach (var revFolder in _fileService.GetRevisionFolders(section.SectionPath))
            {
                var revPath = Path.Combine(section.SectionPath, revFolder);
                var createdTime = Directory.GetCreationTimeUtc(revPath);
                if (createdTime <= since) continue;

                var revNum = int.TryParse(revFolder.AsSpan(1), out var n) ? n : 0;
                var notesPath = Path.Combine(revPath, "revision_notes.txt");
                if (!File.Exists(notesPath)) continue;

                var notesContent = File.ReadAllText(notesPath);
                var noteLines = notesContent.Split('\n');
                var userNote = noteLines.Length > 1
                    ? string.Join(" ", noteLines.Skip(1)).Trim()
                    : noteLines[0].Trim();

                if (!string.IsNullOrWhiteSpace(userNote))
                    parts.Add($"{section.Name} (v{revNum:D3}): {userNote}");
            }
        }

        return string.Join(". ", parts);
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
