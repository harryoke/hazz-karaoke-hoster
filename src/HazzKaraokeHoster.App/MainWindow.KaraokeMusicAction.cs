using System.Windows;
using System.Windows.Controls;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private string _karaokeMusicAction = "Next";
    private string _activeKaraokeMusicAction = "Next";
    private MusicDeckId _retainedMusicDeck;
    private MusicQueueItem? _retainedMusicItem;
    private bool _retainedMusicEnded;

    private void ApplyKaraokeMusicAction(string action)
    {
        _karaokeMusicAction = action is "Pause" or "Muted" ? action : "Next";
        foreach (var item in KaraokeMusicActionMenu.Items.OfType<MenuItem>())
            item.IsChecked = Equals(item.Tag, _karaokeMusicAction);
    }

    private void KaraokeMusicAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        ApplyKaraokeMusicAction(item.Tag?.ToString() ?? "Next");
        SaveMainLayout();
    }

    private void CaptureRetainedMusic()
    {
        _activeKaraokeMusicAction = _karaokeMusicAction;
        _retainedMusicDeck = MusicDeckId.None;
        _retainedMusicItem = null;
        _retainedMusicEnded = false;
        if (_activeKaraokeMusicAction == "Next") return;
        var deck = _crossfadeActive ? _crossfadeTo : _activeMusicDeck;
        if (deck == MusicDeckId.None || IsDeckPaused(deck)) return;
        _retainedMusicItem = CurrentMusicItemFor(deck);
        if (_retainedMusicItem is not null) _retainedMusicDeck = deck;
    }

    private bool FinishRetainedMusicFade()
    {
        if (_retainedMusicDeck == MusicDeckId.None) return false;
        foreach (var deck in new[] { MusicDeckId.Deck1, MusicDeckId.Deck2 })
        {
            SetDeckFadeFactor(deck, 0);
            if (deck != _retainedMusicDeck)
            {
                StopDeck(deck);
                RemovePlayedMusicItemFromPlaylist(deck);
            }
        }
        if (_activeKaraokeMusicAction == "Pause" && !_retainedMusicEnded)
        {
            var media = MediaFor(_retainedMusicDeck);
            SetPausedPosition(_retainedMusicDeck, media.Position);
            media.Pause();
            SetDeckPaused(_retainedMusicDeck, true);
        }
        ClearMusicVideoForDeck(_retainedMusicDeck);
        _musicFadeOutForKaraoke = false;
        UpdateMusicAutomationStatus(_activeKaraokeMusicAction == "Pause"
            ? "Karaoke playing • music paused for return"
            : "Karaoke playing • music continues silently");
        return true;
    }

    private bool ResumeRetainedMusic(double? fadeInSeconds)
    {
        var deck = _retainedMusicDeck;
        var item = _retainedMusicItem;
        _retainedMusicDeck = MusicDeckId.None;
        _retainedMusicItem = null;
        if (!_musicSuspendedForKaraoke || deck == MusicDeckId.None) return false;
        if (_retainedMusicEnded || !ReferenceEquals(CurrentMusicItemFor(deck), item))
        {
            if (ReferenceEquals(CurrentMusicItemFor(deck), item))
            {
                StopDeck(deck);
                RemovePlayedMusicItemFromPlaylist(deck);
            }
            return false;
        }
        _musicFadeOutForKaraoke = false;
        foreach (var other in new[] { MusicDeckId.Deck1, MusicDeckId.Deck2 })
        {
            SetDeckFadeFactor(other, 0);
            if (other != deck) { StopDeck(other); RemovePlayedMusicItemFromPlaylist(other); }
        }
        _musicSuspendedForKaraoke = false;
        _activeMusicDeck = deck;
        if (IsDeckPaused(deck)) ResumePausedMusicDeck(deck);
        ApplyCurrentMusicVideoToAudience();
        _musicResumeFadeSeconds = Math.Max(0.1, fadeInSeconds ?? CrossfadeSeconds);
        _musicFadeInResume = true;
        _musicTransitionStartedUtc = DateTime.UtcNow;
        _resumeMusicDeck = MusicDeckId.None;
        _resumeMusicItem = null;
        _resumeMusicIndex = -1;
        UpdateMusicAutomationStatus($"Returning to {DeckName(deck)} • {_musicResumeFadeSeconds:0.0}s fade in");
        return true;
    }
}
