using System.Collections.ObjectModel;

namespace HazzKaraokeHoster.App;

public sealed class VirtualFolderNode
{
    public long Id { get; init; }
    public long? ParentId { get; init; }
    public string Name { get; set; } = string.Empty;
    public long TrackCount { get; set; }
    public string DisplayText => $"{Name}  ({TrackCount:N0})";
    public ObservableCollection<VirtualFolderNode> Children { get; } = new();
    public bool IsExpanded { get; set; } = true;
    public bool IsSelected { get; set; }
}
