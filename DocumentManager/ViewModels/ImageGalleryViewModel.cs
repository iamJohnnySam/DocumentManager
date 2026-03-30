using System.Collections.ObjectModel;
using System.IO;
using DocumentManager.Models;
using DocumentManager.Services;

namespace DocumentManager.ViewModels;

/// <summary>
/// Manages the image gallery panel showing project and common images.
/// </summary>
public class ImageGalleryViewModel : ViewModelBase
{
    private readonly FileService _fileService;

    public ObservableCollection<ImageAssetModel> Images { get; } = [];
    public ObservableCollection<ImageAssetModel> FilteredImages { get; } = [];

    private ImageAssetModel? _selectedImage;
    public ImageAssetModel? SelectedImage
    {
        get => _selectedImage;
        set => SetProperty(ref _selectedImage, value);
    }

    private string _searchFilter = string.Empty;
    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
                ApplyFilter();
        }
    }

    private string _projectRoot = string.Empty;
    private string _commonRoot = string.Empty;

    public ImageGalleryViewModel(FileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Loads images from the project-specific folder and the shared common images folder.
    /// </summary>
    public void LoadImages(string projectRoot, string commonRoot)
    {
        if (string.IsNullOrEmpty(projectRoot)) return;
        _projectRoot = projectRoot;
        _commonRoot = commonRoot;

        Images.Clear();
        FilteredImages.Clear();

        // Project-specific images (inside the project folder)
        var projectImagesPath = Path.Combine(projectRoot, "images");
        LoadImagesFromPath(projectImagesPath, ImageCategory.Project, projectRoot);

        // Common images (in the shared common root, outside any project)
        if (!string.IsNullOrEmpty(commonRoot))
        {
            var commonImagesPath = Path.Combine(commonRoot, "images");
            LoadImagesFromPath(commonImagesPath, ImageCategory.Common, commonRoot);
        }

        ApplyFilter();
    }

    private void LoadImagesFromPath(string path, ImageCategory category, string projectRoot)
    {
        var files = _fileService.GetImageFiles(path);
        foreach (var file in files)
        {
            Images.Add(new ImageAssetModel
            {
                FileName = Path.GetFileName(file),
                FullPath = file,
                RelativePath = Path.GetRelativePath(projectRoot, file),
                Category = category
            });
        }
    }

    private void ApplyFilter()
    {
        FilteredImages.Clear();
        var filter = _searchFilter.Trim();

        foreach (var image in Images)
        {
            if (string.IsNullOrEmpty(filter) ||
                image.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                FilteredImages.Add(image);
            }
        }
    }

    /// <summary>
    /// Generates \includegraphics LaTeX code for the given image.
    /// </summary>
    public string GetIncludeGraphicsCode(ImageAssetModel image)
    {
        var path = image.RelativePath.Replace('\\', '/');
        return $"\\includegraphics[width=\\linewidth]{{{path}}}";
    }
}
