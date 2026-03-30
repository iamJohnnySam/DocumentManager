using System.IO;
using System.Text.Json;
using DocumentManager.Models;

namespace DocumentManager.Services;

/// <summary>
/// Handles all file I/O operations with thread safety and async support.
/// </summary>
public class FileService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public async Task<string> ReadFileAsync(string path)
    {
        await FileLock.WaitAsync();
        try
        {
            return await File.ReadAllTextAsync(path);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task WriteFileAsync(string path, string content)
    {
        await FileLock.WaitAsync();
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, content);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<ProjectMetadata> LoadMetadataAsync(string projectRoot)
    {
        var metadataPath = Path.Combine(projectRoot, "metadata.json");
        if (!File.Exists(metadataPath))
            return new ProjectMetadata();

        var json = await ReadFileAsync(metadataPath);
        return JsonSerializer.Deserialize<ProjectMetadata>(json, JsonOptions) ?? new ProjectMetadata();
    }

    public async Task SaveMetadataAsync(string projectRoot, ProjectMetadata metadata)
    {
        metadata.UpdatedDate = DateTime.UtcNow;
        var metadataPath = Path.Combine(projectRoot, "metadata.json");
        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        await WriteFileAsync(metadataPath, json);
    }

    /// <summary>
    /// Creates the common root folder structure (sections, images, projects).
    /// Called once on first-time setup.
    /// </summary>
    public void EnsureCommonRootStructure(string commonRoot)
    {
        Directory.CreateDirectory(Path.Combine(commonRoot, "sections"));
        Directory.CreateDirectory(Path.Combine(commonRoot, "images"));
        Directory.CreateDirectory(Path.Combine(commonRoot, "projects"));
    }

    /// <summary>
    /// Creates the project directory structure inside the common root.
    /// Project-specific images stay inside the project folder.
    /// Sections and common images are in the shared common root.
    /// </summary>
    public void CreateProjectStructure(string commonRoot, string projectCode)
    {
        var root = Path.Combine(commonRoot, "projects", projectCode);
        Directory.CreateDirectory(Path.Combine(root, "outer"));
        Directory.CreateDirectory(Path.Combine(root, "images"));
        Directory.CreateDirectory(Path.Combine(root, "templates"));
        Directory.CreateDirectory(Path.Combine(root, "snippets"));
    }

    /// <summary>
    /// Returns all section names from the shared sections folder.
    /// </summary>
    public List<string> GetSharedSections(string commonRoot)
    {
        var sectionsPath = Path.Combine(commonRoot, "sections");
        if (!Directory.Exists(sectionsPath))
            return [];

        return Directory.GetDirectories(sectionsPath)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();
    }

    public List<string> GetRevisionFolders(string sectionPath)
    {
        if (!Directory.Exists(sectionPath))
            return [];

        return Directory.GetDirectories(sectionPath)
            .Where(d => Path.GetFileName(d)?.StartsWith('v') == true)
            .OrderBy(d => d)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();
    }

    public int GetLatestRevisionNumber(string sectionPath)
    {
        var folders = GetRevisionFolders(sectionPath);
        if (folders.Count == 0) return 0;

        return folders
            .Select(f => int.TryParse(f.AsSpan(1), out var n) ? n : 0)
            .Max();
    }

    public List<string> GetFilesInDirectory(string dirPath, string? searchPattern = null)
    {
        if (!Directory.Exists(dirPath))
            return [];

        return Directory.GetFiles(dirPath, searchPattern ?? "*.*").ToList();
    }

    public List<string> GetImageFiles(string imagesRoot)
    {
        if (!Directory.Exists(imagesRoot))
            return [];

        string[] extensions = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".pdf", ".eps", ".svg"];
        return Directory.GetFiles(imagesRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();
    }

    public async Task CopyFileAsync(string source, string destination)
    {
        var dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        await using var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await sourceStream.CopyToAsync(destStream);
    }
}
