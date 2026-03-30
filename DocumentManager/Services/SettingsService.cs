using System.IO;
using System.Text.Json;
using DocumentManager.Models;

namespace DocumentManager.Services;

/// <summary>
/// Loads and saves application-wide settings from local app data.
/// The settings file stores the common root path chosen on first launch.
/// </summary>
public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LaTeXDocumentManager");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    /// <summary>
    /// Returns true when the common root has never been configured.
    /// </summary>
    public bool IsFirstRun => !File.Exists(SettingsPath) || string.IsNullOrEmpty(Load().CommonRootPath);
}
