using System.Text.Json.Serialization;

namespace DocumentManager.Models;

/// <summary>
/// Persisted application settings stored in local app data.
/// Tracks the common root folder shared by all projects.
/// </summary>
public class AppSettings
{
    [JsonPropertyName("commonRootPath")]
    public string CommonRootPath { get; set; } = string.Empty;

    [JsonPropertyName("lastProjectCode")]
    public string LastProjectCode { get; set; } = string.Empty;
}
