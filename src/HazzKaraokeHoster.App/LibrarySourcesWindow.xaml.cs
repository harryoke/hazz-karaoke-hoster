using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;

namespace HazzKaraokeHoster.App;

public partial class LibrarySourcesWindow : Window
{
    private readonly ObservableCollection<string> _folders = new();
    public IReadOnlyList<string> SelectedFolders => _folders.ToList();

    public LibrarySourcesWindow(string title, IEnumerable<string>? initialFolders = null)
    {
        InitializeComponent();
        Title = title;
        Heading.Text = title;
        HintText.Text = "Add as many folders or drive roots as needed. Subfolders are included automatically. Overlapping folders are de-duplicated during indexing.";
        FoldersList.ItemsSource = _folders;
        if (initialFolders is not null)
            foreach (var f in initialFolders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)) _folders.Add(f);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Add library folder", Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        foreach (var folder in dialog.FolderNames.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            if (!_folders.Contains(folder, StringComparer.OrdinalIgnoreCase)) _folders.Add(folder);
    }

    private void RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        var selected = FoldersList.SelectedItems.Cast<string>().ToArray();
        foreach (var item in selected) _folders.Remove(item);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => _folders.Clear();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_folders.Count == 0)
        {
            MessageBox.Show(this, "Add at least one folder first.", "Library Folders", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
