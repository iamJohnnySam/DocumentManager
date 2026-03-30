using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace DocumentManager.Views;

/// <summary>
/// Window that scans the shared sections folder, shows available sections
/// with their latest revision, and lets the user pick which to include.
/// </summary>
public partial class FileBrowserWindow : Window
{
    public ObservableCollection<SharedSectionRow> Rows { get; } = [];

    /// <summary>
    /// Returns the list of section names the user selected for inclusion.
    /// </summary>
    public List<(string Name, int LatestRevision)> SelectedSections { get; } = [];

    public FileBrowserWindow(
        string sharedSectionsPath,
        IReadOnlySet<string> alreadyIncluded)
    {
        InitializeComponent();
        ScanSections(sharedSectionsPath, alreadyIncluded);
        SectionsGrid.ItemsSource = Rows;
    }

    private void ScanSections(string sharedSectionsPath, IReadOnlySet<string> alreadyIncluded)
    {
        if (!Directory.Exists(sharedSectionsPath)) return;

        foreach (var dir in Directory.GetDirectories(sharedSectionsPath))
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name)) continue;

            var revFolders = Directory.GetDirectories(dir)
                .Where(d => Path.GetFileName(d)?.StartsWith('v') == true)
                .OrderBy(d => d)
                .ToList();

            if (revFolders.Count == 0) continue;

            var latestFolder = revFolders[^1];
            var latestRevName = Path.GetFileName(latestFolder) ?? "v001";
            var latestRev = int.TryParse(latestRevName.AsSpan(1), out var n) ? n : 1;

            // Try to read revision notes
            var notesPath = Path.Combine(latestFolder, "revision_notes.txt");
            var notes = File.Exists(notesPath) ? ReadFirstLines(notesPath, 2) : "";

            var isIncluded = alreadyIncluded.Contains(name);

            Rows.Add(new SharedSectionRow
            {
                Name = name,
                LatestRevision = latestRev,
                LatestNotes = notes,
                IsSelected = isIncluded,
                WasAlreadyIncluded = isIncluded
            });
        }
    }

    private static string ReadFirstLines(string path, int count)
    {
        try
        {
            var lines = File.ReadLines(path).Take(count);
            return string.Join(" ", lines).Trim();
        }
        catch { return ""; }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        SelectedSections.Clear();
        foreach (var row in Rows.Where(r => r.IsSelected))
        {
            SelectedSections.Add((row.Name, row.LatestRevision));
        }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// Row model for the shared-sections data grid.
/// </summary>
public class SharedSectionRow : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public int LatestRevision { get; set; }
    public string LatestNotes { get; set; } = string.Empty;
    public bool WasAlreadyIncluded { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string LatestRevisionDisplay => $"v{LatestRevision:D3}";
    public string PinnedRevisionDisplay => WasAlreadyIncluded ? $"v{LatestRevision:D3}" : "—";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
