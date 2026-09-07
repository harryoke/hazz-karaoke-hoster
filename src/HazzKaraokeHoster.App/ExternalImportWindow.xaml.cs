using System.Windows;
using System.Windows.Controls;
using HazzKaraokeHoster.Core.Models;

namespace HazzKaraokeHoster.App;

public partial class ExternalImportWindow : Window
{
    public ExternalMediaKindMode SelectedMediaMode { get; private set; } = ExternalMediaKindMode.Auto;
    public bool VerifyPaths => VerifyPathsCheck.IsChecked == true;

    public ExternalImportWindow(ExternalImportPreview preview)
    {
        InitializeComponent();
        SourceText.Text = $"Detected: {preview.DetectedSource}\n{preview.SourcePath}";
        var records = preview.EstimatedRecords > 0 ? preview.EstimatedRecords.ToString("N0") : "streamed / not pre-counted";
        SummaryText.Text = $"Records: {records}   •   Playlists detected: {preview.PlaylistsDetected:N0}   •   Preview rows: {preview.SampleRows.Count:N0}\n{string.Join("   •   ", preview.DetectedObjects.Take(5))}";
        PreviewGrid.ItemsSource = preview.SampleRows;
        WarningText.Text = preview.Warnings.Count == 0
            ? "Source is opened read-only. Hazz copies records into its own database and never writes to the original hoster database/list."
            : string.Join("\n", preview.Warnings.Take(8));
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (MediaModeCombo.SelectedItem is ComboBoxItem item && Enum.TryParse<ExternalMediaKindMode>(item.Tag?.ToString(), out var mode))
            SelectedMediaMode = mode;
        DialogResult = true;
    }
}
