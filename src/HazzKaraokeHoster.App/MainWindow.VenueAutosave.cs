using System.Text.Json;
using System.IO;
using HazzKaraokeHoster.Data;

namespace HazzKaraokeHoster.App;

public partial class MainWindow
{
    private readonly SemaphoreSlim _venueSaveGate = new(1, 1);
    private Guid? _activeSingerVenue;
    private bool _venueDialogOpen;
    private bool _venueClosing;
    private bool _venueCloseReady;
    private string ActiveSingerVenuePath => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "active-singer-venue.json");

    private void SetActiveSingerVenue(Guid? id)
    {
        var temp = ActiveSingerVenuePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(id));
        File.Move(temp, ActiveSingerVenuePath, true);
        _activeSingerVenue = id;
    }

    private void InitializeVenueAutosave()
    {
        try
        {
            _activeSingerVenue = File.Exists(ActiveSingerVenuePath)
                ? JsonSerializer.Deserialize<Guid?>(File.ReadAllText(ActiveSingerVenuePath)) : null;
        }
        catch (Exception ex) { App.WriteDiagnostic("VENUE AUTOSAVE LOAD", ex.ToString()); }
    }

    private async Task SaveActiveVenueAsync()
    {
        await _venueSaveGate.WaitAsync();
        try
        {
            if (_activeSingerVenue is not Guid active) return;
            var profiles = JsonSerializer.Deserialize<List<VenueProfile>>(File.ReadAllText(VenueProfilesPath)) ?? throw new InvalidDataException("Venue profiles could not be read.");
            var profile = profiles.SingleOrDefault(x => x.Id == active) ?? throw new InvalidDataException("The active venue profile is missing.");
            var roster = CaptureVenueRoster();
            var groups = _fairTurns.ToDictionary(x => x.Key, x => x.Value.Group);
            var id = Guid.NewGuid();
            var directory = Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "venue-singers");
            var path = Path.Combine(directory, id.ToString("N") + ".db");
            await new VenueSingerStore(_db).SaveAsync(path);
            var previousAuto = profile.LastAutomaticSnapshot;
            profile.SingerSnapshot = id; profile.LastAutomaticSnapshot = id;
            profile.SingerRoster = roster; profile.SingerGroups = groups;
            var temp = VenueProfilesPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, VenueProfilesPath, true);
            // Only retire our own previous automatic revision after the new pointer is committed.
            if (previousAuto is Guid old && profiles.All(x => x.SingerSnapshot != old))
            {
                try { File.Delete(Path.Combine(directory, old.ToString("N") + ".db")); }
                catch (Exception ex) { App.WriteDiagnostic("VENUE AUTOSAVE CLEANUP", ex.ToString()); }
            }
        }
        finally { _venueSaveGate.Release(); }
    }
}
