using System.Windows;

namespace HazzKaraokeHoster.App;

public partial class VirtualFolderNameDialog : Window
{
    public string FolderName => NameBox.Text.Trim();

    public VirtualFolderNameDialog(string title, string prompt, string currentName = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        NameBox.Text = currentName;
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (FolderName.Length == 0)
        {
            MessageBox.Show(this, "Enter a folder name.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
