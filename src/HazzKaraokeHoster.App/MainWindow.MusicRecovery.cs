using System.Windows;
namespace HazzKaraokeHoster.App;
public partial class MainWindow
{
    private List<RecoveredMusicDeck> CaptureMusicRecovery()
    {
        var result = new List<RecoveredMusicDeck>();
        foreach (var deck in new[] { MusicDeckId.Deck1, MusicDeckId.Deck2 })
        {
            var item = CurrentMusicItemFor(deck);
            if (item is null) continue;
            result.Add(new() { Deck = deck == MusicDeckId.Deck1 ? 1 : 2, Path = item.FilePath, Artist = item.Artist, Title = item.Title, PositionSeconds = MediaFor(deck).Position.TotalSeconds });
        }
        return result;
    }
    private void RestoreCurrentMusic(IEnumerable<RecoveredMusicDeck> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Path)) continue;
            var deck = snapshot.Deck == 1 ? MusicDeckId.Deck1 : MusicDeckId.Deck2;
            if (_singleDeckMode && deck == MusicDeckId.Deck2) continue;
            var item = new MusicQueueItem { FilePath = snapshot.Path, Artist = snapshot.Artist, Title = snapshot.Title };
            var list = PlaylistFor(deck);
            list.Items.Insert(0, item);
            SetCurrentMusicItem(deck, item);
            SetCurrentIndexAndAdvance(deck, 0);
            SetDeckPaused(deck, true);
            SetDeckFadeFactor(deck, 1.0);
            var position = TimeSpan.FromSeconds(double.IsFinite(snapshot.PositionSeconds) ? Math.Max(0, snapshot.PositionSeconds) : 0);
            if (deck == MusicDeckId.Deck1) _deck1PausedPosition = position; else _deck2PausedPosition = position;
            var media = MediaFor(deck);
            media.Source = new Uri(snapshot.Path);
            media.Position = position;
            // Merely setting the source does not start either audio engine.
            TitleFor(deck).Text = "RECOVERED — press Resume: " + item.DisplayTitle;
            RenumberPlaylist(list);
        }
    }
}
