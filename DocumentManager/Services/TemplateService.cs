using System.IO;

namespace DocumentManager.Services;

/// <summary>
/// Manages LaTeX templates for main documents.
/// </summary>
public class TemplateService
{
    private readonly FileService _fileService;

    public TemplateService(FileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Returns the default main.tex template content.
    /// </summary>
    public string GetDefaultMainTemplate(string title, string author)
    {
        return $@"\documentclass[12pt,a4paper]{{article}}
\usepackage[utf8]{{inputenc}}
\usepackage{{graphicx}}
\usepackage{{hyperref}}
\usepackage{{geometry}}
\geometry{{margin=1in}}

\title{{{title}}}
\author{{{author}}}
\date{{\today}}

\begin{{document}}

\maketitle
\tableofcontents
\newpage

\input{{revision_history}}

% Add your sections below (use absolute or relative paths to the shared sections folder)
% \input{{../../sections/SectionName/v001/content}}

\end{{document}}
";
    }

    /// <summary>
    /// Returns available outer document templates from the common root templates folder.
    /// </summary>
    public List<string> GetAvailableOuterTemplates(string commonRoot)
    {
        var dir = Path.Combine(commonRoot, "templates");
        if (!Directory.Exists(dir))
            return [];

        return Directory.GetFiles(dir, "*.tex")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();
    }

    /// <summary>
    /// Loads a template from the common root templates folder.
    /// </summary>
    public async Task<string> LoadOuterTemplateAsync(string commonRoot, string templateName)
    {
        var path = Path.Combine(commonRoot, "templates", $"{templateName}.tex");
        return File.Exists(path) ? await _fileService.ReadFileAsync(path) : string.Empty;
    }

    /// <summary>
    /// Saves content as a named template in the common root templates folder.
    /// </summary>
    public async Task SaveOuterTemplateAsync(string commonRoot, string templateName, string content)
    {
        var dir = Path.Combine(commonRoot, "templates");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{templateName}.tex");
        await _fileService.WriteFileAsync(path, content);
    }

    public List<string> GetAvailableTemplates(string projectRoot)
    {
        var templatesDir = Path.Combine(projectRoot, "templates");
        if (!Directory.Exists(templatesDir))
            return [];

        return Directory.GetFiles(templatesDir, "*.tex")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Cast<string>()
            .ToList();
    }

    public async Task<string> LoadTemplateAsync(string projectRoot, string templateName)
    {
        var path = Path.Combine(projectRoot, "templates", $"{templateName}.tex");
        return File.Exists(path) ? await _fileService.ReadFileAsync(path) : string.Empty;
    }

    public async Task SaveTemplateAsync(string projectRoot, string templateName, string content)
    {
        var path = Path.Combine(projectRoot, "templates", $"{templateName}.tex");
        await _fileService.WriteFileAsync(path, content);
    }
}
