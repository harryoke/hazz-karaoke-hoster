using System.Windows;
using System.Windows.Input;

namespace HazzKaraokeHoster.App;

public partial class PlaylistNameDialog : Window
{
    public string PlaylistName => PlaylistNameBox.Text.Trim();

    public PlaylistNameDialog(string suggestedName)
    {
        InitializeComponent();
        PlaylistNameBox.Text = suggestedName;
        Loaded += (_, _) =>
        {
            PlaylistNameBox.Focus();
            PlaylistNameBox.SelectAll();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistName.Length == 0)
        {
            MessageBox.Show(this, "Enter a name for the playlist.", "Save Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            PlaylistNameBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void PlaylistNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Save_Click(sender, e);
        e.Handled = true;
    }
}
