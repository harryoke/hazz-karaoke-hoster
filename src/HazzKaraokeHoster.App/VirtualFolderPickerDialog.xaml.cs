using System.Windows;
using System.Windows.Input;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public sealed record VirtualFolderChoice(long Id, string DisplayName);

public partial class VirtualFolderPickerDialog : Window
{
    public long? SelectedFolderId => (FolderList.SelectedItem as VirtualFolderChoice)?.Id;

    public VirtualFolderPickerDialog(IReadOnlyList<VirtualFolder> folders, long? preferredFolderId, int trackCount)
    {
        InitializeComponent();
        PromptText.Text = $"Choose the folder for {trackCount:N0} selected track(s):";
        var byId = folders.ToDictionary(x => x.Id);
        string PathFor(VirtualFolder folder)
        {
            var names = new List<string> { folder.Name };
            var parent = folder.ParentId;
            var guard = new HashSet<long> { folder.Id };
            while (parent is long id && guard.Add(id) && byId.TryGetValue(id, out var item))
            {
                names.Add(item.Name);
                parent = item.ParentId;
            }
            names.Reverse();
            return string.Join("  >  ", names);
        }
        var choices = folders.Select(x => new VirtualFolderChoice(x.Id, PathFor(x)))
            .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        FolderList.ItemsSource = choices;
        FolderList.SelectedItem = choices.FirstOrDefault(x => x.Id == preferredFolderId) ?? choices.FirstOrDefault();
        Loaded += (_, _) => FolderList.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFolderId is null)
        {
            MessageBox.Show(this, "Choose a virtual folder.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void FolderList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => Add_Click(sender, e);
}
