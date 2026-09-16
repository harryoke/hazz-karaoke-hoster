using System.Windows;
using System.Windows.Media.Imaging;
using HazzKaraokeHoster.Core.Models;
using Microsoft.Win32;
using System.Diagnostics;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    public static readonly DependencyProperty SingerPhotoVenueProperty = DependencyProperty.Register(nameof(SingerPhotoVenue), typeof(string), typeof(MainWindow), new PropertyMetadata("default"));
    public static readonly DependencyProperty SingerPhotoRevisionProperty = DependencyProperty.Register(nameof(SingerPhotoRevision), typeof(int), typeof(MainWindow), new PropertyMetadata(0));
    public string SingerPhotoVenue { get => (string)GetValue(SingerPhotoVenueProperty); set => SetValue(SingerPhotoVenueProperty, value); }
    public int SingerPhotoRevision { get => (int)GetValue(SingerPhotoRevisionProperty); set => SetValue(SingerPhotoRevisionProperty, value); }

    private async void SingerPhotoChoose_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        var dialog = new OpenFileDialog { Title = "Choose a photo for " + singer.SingerName, Filter = "Pictures|*.jpg;*.jpeg;*.png;*.bmp;*.gif" };
        if (dialog.ShowDialog(this) != true) return;
        var path = SingerPortrait.PhotoPath(SingerPhotoVenue, singer.SingerName);
        try
        {
            await SaveSingerThumbnailAsync(dialog.FileName, path);
            SingerPortrait.InvalidatePhotoCache(); SingerPhotoRevision++;
            UpdateAudienceNext(); ApplyOverlaySettings();
            QueueDragHint.Text = "Photo saved for " + singer.SingerName + ". It will return with this venue's singer list.";
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not save the singer photo.\n\n" + ex.Message, "Singer Photo"); }
    }
    internal static Task SaveSingerThumbnailAsync(string source, string path) => Task.Run(() =>
    {
        if (new FileInfo(source).Length > 30 * 1024 * 1024) throw new InvalidDataException("Please choose a picture smaller than 30 MB.");
        // Thumbnail decoding bounds memory, leaves the original untouched, and
        // releases the file before the UI displays the saved JPEG.
        using var input = File.OpenRead(source);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 384; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze();
        if (bitmap.PixelHeight > 4096) throw new InvalidDataException("Please choose a normal portrait or landscape photograph.");
        BitmapSource thumbnail = bitmap;
        if (bitmap.PixelHeight > 384)
        {
            var scaled = new System.Windows.Media.Imaging.TransformedBitmap(bitmap, new System.Windows.Media.ScaleTransform(384d / bitmap.PixelHeight, 384d / bitmap.PixelHeight));
            scaled.Freeze(); thumbnail = scaled;
        }
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 }; encoder.Frames.Add(BitmapFrame.Create(thumbnail));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var output = File.Create(temp)) encoder.Save(output); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    });
    private void SingerPhotoRemove_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        try
        {
            File.Delete(SingerPortrait.PhotoPath(SingerPhotoVenue, singer.SingerName));
            SingerPortrait.InvalidatePhotoCache(); SingerPhotoRevision++;
            UpdateAudienceNext(); ApplyOverlaySettings();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Remove Singer Photo"); }
    }
    private async void SingerPhotoWebcam_Click(object sender, RoutedEventArgs e)
    {
        if (QueueList.SelectedItem is not SingerQueueEntry singer) return;
        try
        {
            Process.Start(new ProcessStartInfo("microsoft.windows.camera:") { UseShellExecute = true });
            MessageBox.Show(this, "The Windows Camera app is opening. Take the picture, save it, then choose the saved picture in the next dialog.", "Webcam Singer Photo", MessageBoxButton.OK, MessageBoxImage.Information);
            var dialog = new OpenFileDialog { Title = "Choose the webcam photo for " + singer.SingerName, Filter = "Pictures|*.jpg;*.jpeg;*.png;*.bmp" };
            if (dialog.ShowDialog(this) != true) return;
            await SaveSingerThumbnailAsync(dialog.FileName, SingerPortrait.PhotoPath(SingerPhotoVenue, singer.SingerName));
            SingerPortrait.InvalidatePhotoCache(); SingerPhotoRevision++;
            UpdateAudienceNext(); ApplyOverlaySettings();
            QueueDragHint.Text = "Webcam photo saved for " + singer.SingerName + ".";
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not use the webcam photo.\n\n" + ex.Message, "Webcam Singer Photo", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void RenameSingerPhoto(string oldName, string newName)
    {
        try
        {
            var oldPath = SingerPortrait.PhotoPath(SingerPhotoVenue, oldName);
            var newPath = SingerPortrait.PhotoPath(SingerPhotoVenue, newName);
            if (oldPath != newPath && File.Exists(oldPath) && !File.Exists(newPath)) File.Move(oldPath, newPath);
            SingerPortrait.InvalidatePhotoCache(); SingerPhotoRevision++;
        }
        catch (Exception ex) { App.WriteDiagnostic("SINGER PHOTO RENAME", ex.Message); }
    }
}
