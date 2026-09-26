using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HazzKaraokeHoster.App;
using HazzKaraokeHoster.Core.Models;
using HazzKaraokeHoster.Data;
using Microsoft.Data.Sqlite;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object? Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
    static void Check(bool pass, string name) { if (!pass) throw new Exception(name); Console.WriteLine("PASS " + name); }
    [STAThread]
    static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            AudienceWindow? audience = null;
            try
            {
                await DatabaseChecks();
                var settingsType = typeof(AudienceWindow).Assembly.GetType("HazzKaraokeHoster.App.AudienceEnhancementSettings")!;
                var settings = Activator.CreateInstance(settingsType)!;
                settingsType.GetProperty("OverlayTransition")!.SetValue(settings, "Cut");
                settingsType.GetProperty("ShowSingerCallUp")!.SetValue(settings, false);
                settingsType.GetProperty("ShowVenueHeader")!.SetValue(settings, true);
                var styles = AudienceTextStyle.Defaults();
                styles["Call-up message"].Text = "Please welcome our next performer";
                styles["Call-up singer"].Size = 110;
                styles["Call-up singer"].Colour = "#FF00FF00";
                settingsType.GetProperty("TextStyles")!.SetValue(settings, styles);
                audience = new AudienceWindow(); // Deliberately never shown: preview-only timer regression.
                Call(audience, "ApplyEnhancementSettings", settings);
                Call(audience, "ShowSingerCallUp", "Test Singer", "Test Song", TimeSpan.FromSeconds(3), true);
                var card = (Border)audience.FindName("SingerCallUpPanel");
                Check(card.Visibility == Visibility.Visible, "manual call-up works with automatic call-up disabled");
                var name = (TextBlock)audience.FindName("SingerCallUpNameText");
                Check(name.Text == "Test Singer" && name.FontSize == 110 && ((SolidColorBrush)name.Foreground).Color == Colors.Lime, "custom call-up text/font/colour applied");
                Check(((TextBlock)audience.FindName("SingerCallUpMessageText")).Text.Contains("welcome"), "custom call-up instruction");
                await Task.Delay(3300);
                Check(card.Visibility == Visibility.Collapsed, "call-up timer expires without AudienceWindow.Show");
                Call(audience, "SetBroadcastStatus", "5 singers", "Test Venue");
                Check(((Border)audience.FindName("QueueStatusPanel")).Visibility == Visibility.Visible, "idle queue visible");
                audience.SetKaraokeActive(true);
                Check(((Border)audience.FindName("QueueStatusPanel")).Visibility == Visibility.Collapsed, "queue hides immediately on karaoke play");
                Call(audience, "ShowNowSinging", "Singer", "Song", TimeSpan.FromSeconds(3));
                Check(((Border)audience.FindName("NowSingingPanel")).Visibility == Visibility.Visible, "now singing visible during playback");
                audience.SetKaraokeActive(false);
                Check(((Border)audience.FindName("NowSingingPanel")).Visibility == Visibility.Collapsed, "stop immediately clears now singing");
                audience.ShowKamikazeBanner();
                Check(((Border)audience.FindName("QueueStatusPanel")).Visibility == Visibility.Collapsed, "Kamikaze suppresses queue overlays");
                audience.HideKamikazeBanner();
                var image = (Image)audience.FindName("SingerBackgroundImage");
                image.Visibility = Visibility.Visible; image.Opacity = 0.8;
                Call(audience, "AnimateSlideshowElement", image);
                await Task.Delay(850);
                image.Opacity = 0.25;
                Check(Math.Abs(image.Opacity - 0.25) < 0.001, "slideshow animation releases opacity for transparent CDG settings");
                var corrupt = AudienceTextStyle.Defaults(); corrupt["Call-up singer"].Size = double.NaN; corrupt["Call-up singer"].Colour = "not-a-colour";
                var repaired = AudienceTextStyle.Normalize(corrupt);
                Check(double.IsFinite(repaired["Call-up singer"].Size) && repaired["Call-up singer"].Colour == "#FFFFFFFF", "invalid style settings safely normalized");
                Console.WriteLine("All review checks passed; no live show database/settings written.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { audience?.Close(); app.Shutdown(Environment.ExitCode); }
        };
        app.Run();
    }
    private static async Task DatabaseChecks()
    {
        var dir = System.IO.Directory.CreateTempSubdirectory("hazz-review-");
        try
        {
            var db = new HazzDatabase(System.IO.Path.Combine(dir.FullName, "test.db"));
            await db.InitializeAsync();
            using (var connection = new SqliteConnection(db.ConnectionString))
            {
                await connection.OpenAsync(); using var transaction = connection.BeginTransaction();
                using var cmd = connection.CreateCommand(); cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO songs(artist,title,file_path,format,media_kind,disc_id) VALUES('Gnome',$title,$path,'ZIP',$kind,$disc);";
                for (var i=0;i<351;i++)
                {
                    cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$title", "Song " + i); cmd.Parameters.AddWithValue("$path", "C:/tests/" + i + (i==350 ? ".mp4" : ".zip"));
                    cmd.Parameters.AddWithValue("$kind", i==350 ? "Music" : "Karaoke"); cmd.Parameters.AddWithValue("$disc", "Gnome " + i); await cmd.ExecuteNonQueryAsync();
                }
                transaction.Commit();
            }
            db = new HazzDatabase(db.DatabasePath); await db.InitializeAsync();
            var repo = new LibraryRepository(db);
            Check((await repo.SearchByKindAsync("Gnome", "Karaoke", 20001)).Count == 350, "broad search retains tracks beyond old 300 limit");
            Check((await repo.SearchByKindAsync("Gnome", "Music", 20001)).Count == 0 && (await repo.SearchByKindAsync("Gnome", "MusicVideo", 20001)).Count == 1, "legacy music videos migrate and searches stay separate");
            var compare = typeof(MainWindow).GetMethod("CompareNaturalText", BindingFlags.Static | BindingFlags.NonPublic)!;
            Check((int)compare.Invoke(null, new object[]{"Gnome 2", "Gnome 10"})! < 0, "number-aware Disc sorting retained");
            var singer = new SingerQueueEntry { SingerName = "No song yet" };
            singer.SetRotationStanding(2, 8, false);
            Check(singer.RotationPositionText == "2 / 8", "rotation standing retained for a singer without songs");
        }
        finally { SqliteConnection.ClearAllPools(); dir.Delete(true); }
    }
}
