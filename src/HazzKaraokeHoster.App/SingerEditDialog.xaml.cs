using System.Windows;

namespace HazzKaraokeHoster.App;

public partial class SingerEditDialog : Window
{
    public string SingerName => NameBox.Text.Trim();
    public string Notes => NotesBox.Text.Trim();

    public SingerEditDialog(string singerName, string? notes)
    {
        InitializeComponent();
        NameBox.Text = singerName;
        NotesBox.Text = notes ?? string.Empty;
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SingerName.Length == 0)
        {
            MessageBox.Show(this, "Singer name cannot be blank.", "Singer Details", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
