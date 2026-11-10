using System.Windows;
using System.Windows.Controls;
namespace HazzKaraokeHoster.App;
// Isolated host for the production local-file insertion code. No database or players are opened.
public partial class MainWindow : Window
{
    private enum MusicDeckId { None, Deck1, Deck2 }
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Grid MusicDeckAPanel = new(), MusicDeckBPanel = new();
    public readonly ListBox DeckAPlaylist = new(), DeckBPlaylist = new();
    public MusicQueueItem? Current;
    public bool Dirty;
    public string Status = "";
    public MainWindow() { InitializeLocalFileDrops(); }
    private ListBox PlaylistFor(MusicDeckId deck) => deck == MusicDeckId.Deck1 ? DeckAPlaylist : DeckBPlaylist;
    private MusicQueueItem? CurrentMusicItemFor(MusicDeckId deck) => deck == MusicDeckId.Deck1 ? Current : null;
    private MusicQueueItem CreateMusicQueueItem(string path) => MusicQueueItem.FromPath(path);
    private void RecalculateMusicDeckOrder(MusicDeckId deck) { int n=0; foreach(MusicQueueItem item in PlaylistFor(deck).Items) item.Number=++n; }
    private void MarkMusicDeckQueuesDirty() => Dirty=true;
    private void UpdateMusicAutomationStatus(string text) => Status=text;
    private string DeckName(MusicDeckId deck) => deck.ToString();
    public Task TestDrop(string[] files,int index=0,bool deck2=false) => AddLocalMusicFilesAsync(deck2?DeckBPlaylist:DeckAPlaylist,deck2?MusicDeckId.Deck2:MusicDeckId.Deck1,files,index);
}
