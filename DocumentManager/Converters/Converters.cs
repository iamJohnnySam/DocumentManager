using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DocumentManager.Converters;

/// <summary>
/// Converts a boolean to a Visibility value. True = Visible, False = Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Converts a boolean to a Brush. True = highlight color, False = default.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true
            ? new SolidColorBrush(Color.FromRgb(255, 200, 100))
            : new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return false;
    }
}

/// <summary>
/// Converts a ProjectTreeNodeType to an icon string for display.
/// </summary>
public class NodeTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Models.ProjectTreeNodeType nodeType)
            return "📄";

        return nodeType switch
        {
            Models.ProjectTreeNodeType.Root => "📁",
            Models.ProjectTreeNodeType.OuterFolder => "📋",
            Models.ProjectTreeNodeType.SectionsFolder => "📂",
            Models.ProjectTreeNodeType.SharedSectionsFolder => "📂",
            Models.ProjectTreeNodeType.Section => "📑",
            Models.ProjectTreeNodeType.IncludedFile => "📑",
            Models.ProjectTreeNodeType.RevisionFolder => "🔖",
            Models.ProjectTreeNodeType.File => "📄",
            Models.ProjectTreeNodeType.ImagesFolder => "🖼️",
            Models.ProjectTreeNodeType.ProjectImagesFolder => "🖼️",
            Models.ProjectTreeNodeType.CommonImagesFolder => "🌐",
            Models.ProjectTreeNodeType.TemplatesFolder => "📝",
            Models.ProjectTreeNodeType.SnippetsFolder => "✂️",
            _ => "📄"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
