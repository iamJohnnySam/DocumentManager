namespace DocumentManager.Models;

/// <summary>
/// Represents an image asset in the project or common image folders.
/// </summary>
public class ImageAssetModel
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public ImageCategory Category { get; set; }
}

public enum ImageCategory
{
    Project,
    Common
}
