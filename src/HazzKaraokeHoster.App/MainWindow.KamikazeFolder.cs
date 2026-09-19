using System.Windows;
using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using HazzKaraokeHoster.Playback;

namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private string _kamikazeFolderPath = string.Empty;

    private void KamikazeFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_kamikazePickInProgress) { UpdateKamikazeSourceMenu(); return; }
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose Kamikaze songs folder (this folder only)" };
        if (dialog.ShowDialog(this) != true) { UpdateKamikazeSourceMenu(); return; }
        _kamikazeFolderPath = dialog.FolderName;
        _kamikazeLastPath = string.Empty;
        UpdateKamikazeSourceMenu();
        SaveMainLayout();
        SearchStatus.Text = "Kamikaze uses only: " + _kamikazeFolderPath;
    }

    private void KamikazeLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_kamikazePickInProgress) { UpdateKamikazeSourceMenu(); return; }
        _kamikazeFolderPath = string.Empty;
        _kamikazeLastPath = string.Empty;
        UpdateKamikazeSourceMenu();
        SaveMainLayout();
    }

    private void UpdateKamikazeSourceMenu()
    {
        KamikazeLibraryMenu.IsChecked = string.IsNullOrWhiteSpace(_kamikazeFolderPath);
        KamikazeFolderMenu.IsChecked = !KamikazeLibraryMenu.IsChecked;
        KamikazeFolderMenu.ToolTip = KamikazeLibraryMenu.IsChecked ? "Choose a folder; subfolders are excluded." : _kamikazeFolderPath;
        KamikazeButton.ToolTip = KamikazeLibraryMenu.IsChecked
            ? "Random karaoke-library song. Choose a restricted folder under SHOW > Kamikaze song source."
            : "Random song from this folder only: " + _kamikazeFolderPath;
    }

    private async Task<SongRecord?> PickFolderKaraokeSongAsync(string? previous, CancellationToken token)
    {
        var path = await KamikazeFolderPicker.PickAsync(_kamikazeFolderPath, previous, token);
        if (path is null) return null; // Never fall back to the library in folder-only mode.
        var existing = await _library.FindByFilePathAsync(path, token);
        if (existing is not null) return existing;
        var parsed = LibraryImportService.ParseName(path);
        var song = new SongRecord(0, parsed.Artist, parsed.Title, parsed.Manufacturer, parsed.DiscId,
            path, Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), 0, DateTimeOffset.UtcNow);
        var id = await _library.UpsertSongAsync(song, token);
        return song with { Id = id };
    }
}
