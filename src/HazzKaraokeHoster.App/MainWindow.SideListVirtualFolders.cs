using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HazzKaraokeHoster.Core.Models;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private const int SideFolderPageSize = 250;
    private bool _sideFolderBrowserVisible;
    private VirtualFolderNode? _sideSelectedFolder;
    private long? _sideSelectedFolderId;
    private LibraryBrowsePage? _sideFolderPage;
    private int _sideFolderOffset;
    private CancellationTokenSource? _sideFolderLoadCts;
    private Point _sideFolderDragStart;
    private SongRecord? _sideFolderDragSong;
    private string _sideFolderMediaKind = "Music";

    private void UpdateSideListVirtualFolderBrowserVisibility()
    {
        if (DeckBSideFolderBrowserPanel is null || SideFolderBrowserButton is null) return;
        var visible = _singleDeckMode && _sideFolderBrowserVisible;
        DeckBSideFolderBrowserPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SideFolderBrowserButton.Content = visible ? "HIDE FOLDERS" : "VIRTUAL FOLDERS";
    }

    private async void SideFolderBrowser_Click(object sender, RoutedEventArgs e)
    {
        _sideFolderBrowserVisible = !_sideFolderBrowserVisible;
        UpdateSideListVirtualFolderBrowserVisibility();
        if (_sideFolderBrowserVisible)
            await ReloadSideListVirtualFoldersAsync();
        if (IsLoaded) SaveMainLayout();
    }

    private void SideFolderClose_Click(object sender, RoutedEventArgs e)
    {
        _sideFolderBrowserVisible = false;
        UpdateSideListVirtualFolderBrowserVisibility();
        if (IsLoaded) SaveMainLayout();
    }

    private async void SideFolderRefresh_Click(object sender, RoutedEventArgs e)
        => await ReloadSideListVirtualFoldersAsync();

    private async void SideFolderAllMusic_Click(object sender, RoutedEventArgs e)
        => await SetSideFolderMediaKindAsync("Music");

    private async void SideFolderAllVideos_Click(object sender, RoutedEventArgs e)
        => await SetSideFolderMediaKindAsync("MusicVideo");

    private async Task SetSideFolderMediaKindAsync(string mediaKind)
    {
        _sideFolderMediaKind = string.Equals(mediaKind, "MusicVideo", StringComparison.OrdinalIgnoreCase) ? "MusicVideo" : "Music";
        if (_sideSelectedFolder is not null) _sideSelectedFolder.IsSelected = false;
        _sideSelectedFolder = null;
        _sideSelectedFolderId = null;
        _sideFolderOffset = 0;
        SideFolderLocationText.Text = _sideFolderMediaKind == "MusicVideo" ? "ALL MUSIC VIDEOS" : "ALL MUSIC";
        await ReloadSideFolderTracksAsync();
    }

    private async Task ReloadSideListVirtualFoldersAsync()
    {
        if (!_sideFolderBrowserVisible) return;
        try
        {
            var folders = await _library.GetVirtualFoldersAsync(_lifetime.Token);
            var nodes = folders.ToDictionary(x => x.Id, x => new VirtualFolderNode
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Name = x.Name,
                TrackCount = x.TrackCount,
                IsSelected = x.Id == _sideSelectedFolderId
            });
            var roots = new List<VirtualFolderNode>();
            foreach (var folder in folders)
            {
                var node = nodes[folder.Id];
                if (folder.ParentId is long parentId && nodes.TryGetValue(parentId, out var parent))
                    parent.Children.Add(node);
                else
                    roots.Add(node);
            }

            SideVirtualFolderTree.ItemsSource = roots;
            _sideSelectedFolder = _sideSelectedFolderId is long id && nodes.TryGetValue(id, out var selected)
                ? selected
                : null;
            if (_sideSelectedFolder is null) _sideSelectedFolderId = null;
            SideFolderLocationText.Text = _sideSelectedFolder is null ? (_sideFolderMediaKind == "MusicVideo" ? "ALL MUSIC VIDEOS" : "ALL MUSIC") : _sideSelectedFolder.Name;
            _sideFolderOffset = 0;
            await ReloadSideFolderTracksAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SideFolderStatusText.Text = "Could not load virtual folders: " + ex.Message;
        }
    }

    private async void SideVirtualFolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not VirtualFolderNode folder) return;
        _sideSelectedFolder = folder;
        _sideSelectedFolderId = folder.Id;
        _sideFolderOffset = 0;
        SideFolderLocationText.Text = folder.Name;
        await ReloadSideFolderTracksAsync();
    }

    private async Task ReloadSideFolderTracksAsync()
    {
        _sideFolderLoadCts?.Cancel();
        _sideFolderLoadCts?.Dispose();
        _sideFolderLoadCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _sideFolderLoadCts.Token;
        try
        {
            var label = _sideFolderMediaKind == "MusicVideo" ? "music videos" : "music";
            SideFolderStatusText.Text = _sideSelectedFolder is null
                ? $"Loading {label} library…"
                : $"Loading {_sideSelectedFolder.Name}…";

            var page = _sideSelectedFolder is null
                ? await _library.BrowseAsync(_sideFolderMediaKind, string.Empty, "Artist", false, _sideFolderOffset, SideFolderPageSize, token)
                : await _library.BrowseVirtualFolderAsync(_sideSelectedFolder.Id, _sideFolderMediaKind, string.Empty, "Artist", false, _sideFolderOffset, SideFolderPageSize, token);
            if (token.IsCancellationRequested) return;

            _sideFolderPage = page;
            _sideFolderOffset = page.Offset;
            SideVirtualFolderTracks.ItemsSource = page.Items;
            SideFolderPageText.Text = $"{page.PageNumber:N0} / {page.PageCount:N0}";
            var itemLabel = _sideFolderMediaKind == "MusicVideo" ? "music video" : "music";
            var location = _sideSelectedFolder is null
                ? (_sideFolderMediaKind == "MusicVideo" ? "all indexed music videos" : "all indexed music")
                : _sideSelectedFolder.Name;
            SideFolderStatusText.Text = page.TotalCount == 0
                ? $"No {itemLabel} tracks found in {location}."
                : $"{page.TotalCount:N0} {itemLabel} track(s) in {location} • showing {page.Offset + 1:N0}–{page.Offset + page.Items.Count:N0}.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SideFolderStatusText.Text = "Virtual-folder browser error: " + ex.Message;
        }
    }

    private async void SideFolderPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (_sideFolderOffset <= 0) return;
        _sideFolderOffset = Math.Max(0, _sideFolderOffset - SideFolderPageSize);
        await ReloadSideFolderTracksAsync();
    }

    private async void SideFolderNext_Click(object sender, RoutedEventArgs e)
    {
        if (_sideFolderPage?.HasNext != true) return;
        _sideFolderOffset += SideFolderPageSize;
        await ReloadSideFolderTracksAsync();
    }

    private SongRecord[] SelectedSideFolderSongs()
    {
        var selected = SideVirtualFolderTracks.SelectedItems.OfType<SongRecord>().ToArray();
        if (selected.Length == 0 && SideVirtualFolderTracks.SelectedItem is SongRecord one)
            selected = new[] { one };
        return selected;
    }

    private int AddSideFolderSongsToPlaylist(IEnumerable<SongRecord> songs, ListBox target, MusicDeckId targetDeck)
    {
        var added = 0;
        var missing = 0;
        MusicQueueItem? first = null;
        foreach (var song in songs.Where(IsMusicDeckMedia))
        {
            if (!File.Exists(song.FilePath))
            {
                BrokenMediaRegistry.Mark(song.FilePath, "File missing or unavailable");
                missing++;
                continue;
            }
            var item = CreateMusicQueueItem(song);
            first ??= item;
            target.Items.Add(item);
            added++;
        }
        RenumberPlaylist(target);
        RecalculateMusicDeckOrder(targetDeck);
        if (first is not null)
        {
            target.SelectedItem = first;
            target.ScrollIntoView(first);
        }
        if (missing > 0)
            SideFolderStatusText.Text = $"Added {added:N0} track(s); skipped {missing:N0} missing file(s).";
        return added;
    }

    private void SideFolderAddToSideList_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedSideFolderSongs();
        if (selected.Length == 0)
        {
            SideFolderStatusText.Text = "Select one or more tracks first.";
            return;
        }
        var added = AddSideFolderSongsToPlaylist(selected, DeckBPlaylist, MusicDeckId.Deck2);
        if (added > 0)
        {
            SideFolderStatusText.Text = $"Added {added:N0} track(s) to the Side List.";
            UpdateMusicAutomationStatus($"Added {added:N0} virtual-folder track(s) to the Side List");
        }
    }

    private void SideFolderSendToDeck1_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedSideFolderSongs();
        if (selected.Length == 0)
        {
            SideFolderStatusText.Text = "Select one or more tracks first.";
            return;
        }
        var added = AddSideFolderSongsToPlaylist(selected, DeckAPlaylist, MusicDeckId.Deck1);
        if (added > 0)
        {
            SideFolderStatusText.Text = $"Sent {added:N0} track(s) to Deck 1.";
            UpdateMusicAutomationStatus($"Added {added:N0} virtual-folder track(s) to Deck 1");
        }
    }

    private void SideFolderTracks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => SideFolderAddToSideList_Click(sender, e);

    private void SideFolderTracks_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _sideFolderDragStart = e.GetPosition(SideVirtualFolderTracks);
        _sideFolderDragSong = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as SongRecord;
    }

    private void SideFolderTracks_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _sideFolderDragSong is null) return;
        var point = e.GetPosition(SideVirtualFolderTracks);
        if (Math.Abs(point.X - _sideFolderDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _sideFolderDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var anchor = _sideFolderDragSong;
        var selected = SideVirtualFolderTracks.SelectedItems.OfType<SongRecord>().ToArray();
        if (!selected.Contains(anchor)) selected = new[] { anchor };
        var data = new DataObject(typeof(SongRecord), anchor);
        data.SetData(SearchSongBatchDataFormat, selected);
        DragDrop.DoDragDrop(SideVirtualFolderTracks, data, DragDropEffects.Copy);
        _sideFolderDragSong = null;
    }
}
