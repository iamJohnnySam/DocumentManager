using System.IO;
using DocumentManager.Models;

namespace DocumentManager.Services;

/// <summary>
/// Manages reusable LaTeX code snippets.
/// </summary>
public class SnippetService
{
    private readonly FileService _fileService;

    public SnippetService(FileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<List<SnippetModel>> LoadSnippetsAsync(string projectRoot)
    {
        var snippetsDir = Path.Combine(projectRoot, "snippets");
        if (!Directory.Exists(snippetsDir))
            return [];

        var snippets = new List<SnippetModel>();
        foreach (var file in Directory.GetFiles(snippetsDir, "*.tex"))
        {
            var content = await _fileService.ReadFileAsync(file);
            snippets.Add(new SnippetModel
            {
                Name = Path.GetFileNameWithoutExtension(file) ?? "Unknown",
                Content = content,
                FilePath = file
            });
        }
        return snippets;
    }

    public async Task SaveSnippetAsync(string projectRoot, string name, string content)
    {
        var path = Path.Combine(projectRoot, "snippets", $"{name}.tex");
        await _fileService.WriteFileAsync(path, content);
    }

    public void DeleteSnippet(string projectRoot, string name)
    {
        var path = Path.Combine(projectRoot, "snippets", $"{name}.tex");
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    /// Returns a set of built-in LaTeX snippets.
    /// </summary>
    public List<SnippetModel> GetBuiltInSnippets()
    {
        return
        [
            new() { Name = "Figure", Content = "\\begin{figure}[htbp]\n  \\centering\n  \\includegraphics[width=\\linewidth]{image}\n  \\caption{Caption}\n  \\label{fig:label}\n\\end{figure}" },
            new() { Name = "Table", Content = "\\begin{table}[htbp]\n  \\centering\n  \\begin{tabular}{|l|l|}\n    \\hline\n    Column 1 & Column 2 \\\\\n    \\hline\n    Data 1 & Data 2 \\\\\n    \\hline\n  \\end{tabular}\n  \\caption{Caption}\n  \\label{tab:label}\n\\end{table}" },
            new() { Name = "Itemize", Content = "\\begin{itemize}\n  \\item Item 1\n  \\item Item 2\n  \\item Item 3\n\\end{itemize}" },
            new() { Name = "Enumerate", Content = "\\begin{enumerate}\n  \\item Item 1\n  \\item Item 2\n  \\item Item 3\n\\end{enumerate}" },
            new() { Name = "Equation", Content = "\\begin{equation}\n  f(x) = ax^2 + bx + c\n  \\label{eq:label}\n\\end{equation}" },
            new() { Name = "Section Input", Content = "\\input{../sections/SectionName/v001/content}" },
            new() { Name = "Include Graphics", Content = "\\includegraphics[width=\\linewidth]{path/to/image}" }
        ];
    }
}
