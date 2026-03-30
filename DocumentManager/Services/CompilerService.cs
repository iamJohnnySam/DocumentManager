using System.Diagnostics;
using System.IO;

namespace DocumentManager.Services;

/// <summary>
/// Detects installed LaTeX compilers and runs compilations.
/// </summary>
public class CompilerService
{
    private static readonly string[] SupportedCompilers = ["pdflatex", "xelatex", "lualatex"];

    /// <summary>
    /// Probes the system for installed LaTeX compilers.
    /// </summary>
    public List<string> DetectInstalledCompilers()
    {
        var installed = new List<string>();
        foreach (var compiler in SupportedCompilers)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = compiler,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process is not null)
                {
                    process.WaitForExit(3000);
                    installed.Add(compiler);
                }
            }
            catch
            {
                // Compiler not available
            }
        }

        return installed;
    }

    /// <summary>
    /// Compiles a .tex file using the specified compiler.
    /// </summary>
    public async Task<CompilationResult> CompileAsync(string texFilePath, string compiler, string outputDir)
    {
        var result = new CompilationResult();
        if (!File.Exists(texFilePath))
        {
            result.Success = false;
            result.ErrorOutput = $"File not found: {texFilePath}";
            return result;
        }

        Directory.CreateDirectory(outputDir);

        var psi = new ProcessStartInfo
        {
            FileName = compiler,
            Arguments = $"-interaction=nonstopmode -output-directory=\"{outputDir}\" \"{texFilePath}\"",
            WorkingDirectory = Path.GetDirectoryName(texFilePath) ?? outputDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            result.Success = process.ExitCode == 0;
            result.StandardOutput = outputBuilder.ToString();
            result.ErrorOutput = errorBuilder.ToString();
            result.ExitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorOutput = $"Compilation failed: {ex.Message}";
        }

        return result;
    }
}

public class CompilationResult
{
    public bool Success { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string ErrorOutput { get; set; } = string.Empty;
    public int ExitCode { get; set; }
}
