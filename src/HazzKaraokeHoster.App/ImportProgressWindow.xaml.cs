using System.ComponentModel;
using System.Windows;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class ImportProgressWindow : Window
{
    private readonly CancellationTokenSource _cancel = new();
    private bool _allowClose;

    public CancellationToken CancellationToken => _cancel.Token;

    public ImportProgressWindow(string title)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
    }

    public void Update(BpmStudioImportProgress value)
    {
        PhaseText.Text = value.Phase;
        FileText.Text = string.IsNullOrWhiteSpace(value.CurrentFile) ? " " : value.CurrentFile;
        ItemsText.Text = value.ItemsProcessed.ToString("N0");
        LinkedText.Text = value.MusicTracksLinkedOrIndexed.ToString("N0");

        if (value.TotalFiles > 0)
        {
            ImportProgressBar.IsIndeterminate = false;
            ImportProgressBar.Value = value.Percent;
            FilesText.Text = $"{value.FilesProcessed:N0} / {value.TotalFiles:N0}";
            PercentText.Text = value.Percent >= 100
                ? "100%"
                : $"{Math.Floor(value.Percent):0}%";
        }
        else
        {
            ImportProgressBar.IsIndeterminate = true;
            FilesText.Text = "Scanning…";
            PercentText.Text = "…";
        }

        StatusText.Text = value.UnsupportedFiles > 0
            ? $"Skipped/unreadable list files: {value.UnsupportedFiles:N0}"
            : "The Hazz main window remains usable while this import runs.";
    }

    public void Complete(string status = "Import complete")
    {
        ImportProgressBar.IsIndeterminate = false;
        ImportProgressBar.Value = 100;
        PercentText.Text = "100%";
        PhaseText.Text = status;
        CancelButton.Content = "CLOSE";
        _allowClose = true;
    }

    public void CloseAfterImport()
    {
        _allowClose = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_allowClose)
        {
            Close();
            return;
        }

        if (!_cancel.IsCancellationRequested)
        {
            _cancel.Cancel();
            CancelButton.IsEnabled = false;
            PhaseText.Text = "Cancelling…";
            StatusText.Text = "Finishing the current database operation safely.";
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            if (!_cancel.IsCancellationRequested)
            {
                _cancel.Cancel();
                CancelButton.IsEnabled = false;
                PhaseText.Text = "Cancelling…";
                StatusText.Text = "Finishing the current database operation safely.";
            }
            return;
        }
        base.OnClosing(e);
    }
}
