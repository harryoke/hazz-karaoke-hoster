using HazzKaraokeHoster.App;
MainWindow.Check();
namespace System.Windows { public class RoutedEventArgs : EventArgs {} }
namespace System.Windows.Controls {
 public class MenuItem { public object? Tag; public bool IsChecked; public List<object> Items = new(); }
}
namespace HazzKaraokeHoster.App {
public class MusicQueueItem {}
public class FakeMedia { public TimeSpan Position; public bool Paused; public void Pause() => Paused = true; }
public partial class MainWindow {
 enum MusicDeckId { None, Deck1, Deck2 }
 private System.Windows.Controls.MenuItem KaraokeMusicActionMenu = new();
 private MusicDeckId _crossfadeTo, _activeMusicDeck, _resumeMusicDeck;
 private bool _crossfadeActive, _musicSuspendedForKaraoke, _musicFadeOutForKaraoke, _musicFadeInResume;
 private MusicQueueItem? _resumeMusicItem;
 private int _resumeMusicIndex;
 private double _musicResumeFadeSeconds;
 private DateTime _musicTransitionStartedUtc;
 private double CrossfadeSeconds => 4;
 private Dictionary<MusicDeckId, FakeMedia> media = new() { [MusicDeckId.Deck1] = new(), [MusicDeckId.Deck2] = new() };
 private Dictionary<MusicDeckId, MusicQueueItem?> items = new();
 private Dictionary<MusicDeckId, double> volumes = new();
 private void SaveMainLayout() {}
 private bool IsDeckPaused(MusicDeckId d) => media[d].Paused;
 private MusicQueueItem? CurrentMusicItemFor(MusicDeckId d) => items.GetValueOrDefault(d);
 private FakeMedia MediaFor(MusicDeckId d) => media[d];
 private void SetDeckFadeFactor(MusicDeckId d, double v) => volumes[d] = v;
 private void StopDeck(MusicDeckId d) { media[d].Position = TimeSpan.Zero; media[d].Paused = false; }
 private void RemovePlayedMusicItemFromPlaylist(MusicDeckId d) => items[d] = null;
 private void SetPausedPosition(MusicDeckId d, TimeSpan t) {}
 private void SetDeckPaused(MusicDeckId d, bool p) => media[d].Paused = p;
 private void ClearMusicVideoForDeck(MusicDeckId d) {}
 private void UpdateMusicAutomationStatus(string s) {}
 private void ResumePausedMusicDeck(MusicDeckId d) => media[d].Paused = false;
 private void ApplyCurrentMusicVideoToAudience() {}
 private string DeckName(MusicDeckId d) => d.ToString();
 private static void Require(bool ok, string text) { if(!ok) throw new Exception(text); }
 private static MainWindow Setup(string mode) {
  var m = new MainWindow();
  m.ApplyKaraokeMusicAction(mode);
  m._activeMusicDeck = MusicDeckId.Deck1;
  m.items[MusicDeckId.Deck1] = new();
  m.media[MusicDeckId.Deck1].Position = TimeSpan.FromSeconds(42);
  m.CaptureRetainedMusic();
  m._musicSuspendedForKaraoke = true;
  return m;
 }
 public static void Check() {
  foreach(var mode in new[] {"Pause","Muted"}) {
   var m=Setup(mode); var item=m.items[MusicDeckId.Deck1];
   Require(m.FinishRetainedMusicFade(),"retention not used");
   Require(m.media[MusicDeckId.Deck1].Paused==(mode=="Pause"),"pause state");
   Require(m.volumes[MusicDeckId.Deck1]==0,"not silent");
   if(mode=="Muted") m.media[MusicDeckId.Deck1].Position=TimeSpan.FromSeconds(90);
   m.ApplyKaraokeMusicAction("Next");
   Require(m.ResumeRetainedMusic(1.5),"resume not handled");
   Require(ReferenceEquals(item,m.items[MusicDeckId.Deck1]),"track removed");
   Require(m.media[MusicDeckId.Deck1].Position.TotalSeconds==(mode=="Pause"?42:90),"position lost");
   Require(!m.media[MusicDeckId.Deck1].Paused && m._musicFadeInResume && m._musicResumeFadeSeconds==1.5,"return fade");
  }
  var original=Setup("Next"); Require(!original.FinishRetainedMusicFade()&&!original.ResumeRetainedMusic(null),"default overridden");
  var ended=Setup("Muted"); ended._retainedMusicEnded=true;
  Require(!ended.ResumeRetainedMusic(null) && ended.items[MusicDeckId.Deck1]==null,"ended track replayed");
  var early=Setup("Pause"); Require(early.ResumeRetainedMusic(1.5)&&early.media[MusicDeckId.Deck1].Position.TotalSeconds==42,"early return lost position");
  var cross=Setup("Pause"); cross._crossfadeActive=true; cross._crossfadeTo=MusicDeckId.Deck2; cross.items[MusicDeckId.Deck2]=new(); cross.CaptureRetainedMusic();
  cross.FinishRetainedMusicFade();
  Require(cross.items[MusicDeckId.Deck1]==null&&cross.IsDeckPaused(MusicDeckId.Deck2),"crossfade retained wrong track");
  Console.WriteLine("PASS pause, silent continuation, original default, early return, ended-track fallback, crossfade and setting changes");
 }
}}
