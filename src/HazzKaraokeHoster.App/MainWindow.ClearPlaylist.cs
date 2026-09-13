using System.Windows;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private void ClearDeckPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var deck = (sender as FrameworkElement)?.Tag as string == "Deck1" ? MusicDeckId.Deck1 : MusicDeckId.Deck2;
        var count = PlaylistFor(deck).Items.Count;
        if (count == 0) { UpdateMusicAutomationStatus(DeckName(deck) + " playlist is already empty"); return; }
        if (MessageBox.Show(this, $"Clear all {count:N0} tracks from {DeckName(deck)}?\n\nThe playing song will continue. Music files and saved playlists are kept.",
            "Clear Playlist", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            ClearMusicPlaylist(deck);
    }

    private void ClearMusicPlaylist(MusicDeckId deck)
    {
        PlaylistFor(deck).Items.Clear();
        RecalculateMusicDeckOrder(deck);
        if (_resumeMusicDeck == deck)
        {
            _resumeMusicDeck = MusicDeckId.None;
            _resumeMusicItem = null;
            _resumeMusicIndex = -1;
        }
        UpdateNextCue();
        MarkMusicDeckQueuesDirty();
        UpdateMusicAutomationStatus(DeckName(deck) + " playlist cleared • playing audio continues");
    }
}
