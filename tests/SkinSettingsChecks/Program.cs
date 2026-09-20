using System.Text.Json.Nodes;
using HazzKaraokeHoster.App;

var directory=Path.Combine(Path.GetTempPath(),"HazzSkinSettingsChecks",Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var path=Path.Combine(directory,"ui-layout.json");
var favouritesPath=Path.Combine(directory,"music-favourites.json");
var favourites=new MusicFavourites(favouritesPath);
favourites.Set(new[]{Path.Combine(directory,"favourite-song.mp3")},true);
var queuesPath=Path.Combine(directory,"music-deck-queues.json");
var queues=new MusicDeckQueueState { Deck1 = new() { new() { FilePath="song-a.mp3", Title="Keep A" } }, Deck2 = new() { new() {FilePath="song-b.mp3",Title="Keep B"} } };
MusicDeckQueueStateStore.SaveTo(queuesPath,queues);
var preserved=new Dictionary<string,byte[]> { [favouritesPath]=File.ReadAllBytes(favouritesPath),[queuesPath]=File.ReadAllBytes(queuesPath) };
var settings=new UiLayoutSettings {
 SavedAvSync=new() { [@"D:\song.mp4"]=-1.25 },
 CdgPresentation=new() { Enabled=true, BackgroundColour=3, BackgroundOpacity=0.45, LyricsOpacity=0.85 },
 CdgSongPresentation=new() { [@"D:\song.zip"]=new() { Enabled=true, BackgroundColour=7 } },
 AudienceKaraokeSizing="Stretch", AudienceScrollerRotationEnabled=false, AudienceScrollerMessageEnabled=true,
 AudienceSecondScrollerEnabled=true, AudienceSecondScrollerText="Drinks offers", AudienceSecondScrollerFontFamily="Arial",
 AudienceSecondScrollerFontSize=96, AudienceSecondScrollerSpeed=180, AudienceSecondScrollerInset=80, AudienceSecondScrollerColor="#FF00FF00",
 KamikazeFolderPath=@"D:\Five-song pool",
 AudienceBackgroundEnabled=true, AudienceBackgroundFolderPath=@"D:\Venue images",
 AudienceLogoEnabled=true, AudienceLogoImagePath=@"D:\Venue logo.png",
 AudienceNextSingerFontFamily="American Captain", AudienceNextSingerFontSize=128,
 AudienceScrollerFontFamily="Coolsville", AudienceScrollerPosition="Top",
 AudienceScrollerEdgeInset=75, AudienceScrollerText="Keep this venue message",
 AudienceBackgroundGifSpeed=0.5, AudienceSingerPhotoSize=96,
 AudienceKamikazeFontFamily="ChunkFive", MusicVideoShowLogo=true
};
UiLayoutSettingsStore.SaveTo(path,settings);
var original=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
original["FutureSetting"]=new JsonObject { ["NestedValue"]="preserve unknown fields" };
File.WriteAllText(path,original.ToJsonString());
foreach(var skin in new[]{"Daylight","Midnight","Copper","Classic","Daylight"}) {
 UiLayoutSettingsStore.SaveSkinTo(path,skin);
 var updated=JsonNode.Parse(File.ReadAllText(path))!.AsObject();
 foreach(var property in original.Where(p=>p.Key!="ConsoleSkin"))
  if(!JsonNode.DeepEquals(property.Value,updated[property.Key])) throw new Exception("Skin changed "+property.Key);
 if(updated["ConsoleSkin"]!.GetValue<string>()!=skin) throw new Exception("Skin not saved");
 foreach(var file in preserved)
  if(!File.ReadAllBytes(file.Key).SequenceEqual(file.Value)) throw new Exception("Skin changed unrelated data: "+file.Key);
}
var fullSave=UiLayoutSettingsStore.LoadFrom(path);
UiLayoutSettingsStore.SaveTo(path,fullSave);
if(JsonNode.Parse(File.ReadAllText(path))!["FutureSetting"]?["NestedValue"]?.GetValue<string>()!="preserve unknown fields") throw new Exception("Full save lost newer setting");
File.WriteAllText(path,"{interrupted write");
var recovered=UiLayoutSettingsStore.LoadFrom(path);
if(recovered.AudienceNextSingerFontFamily!="American Captain" || recovered.AudienceBackgroundFolderPath!=settings.AudienceBackgroundFolderPath)
 throw new Exception("Recovery lost audience settings");
var backup=File.ReadAllText(path+".previous");
UiLayoutSettingsStore.SaveTo(path,recovered);
if(File.ReadAllText(path+".previous")!=backup || !File.Exists(path+".unreadable")) throw new Exception("Corrupt file replaced good recovery copy");
File.WriteAllText(path,"invalid");File.WriteAllText(path+".previous","also invalid");
try {UiLayoutSettingsStore.LoadFrom(path);throw new Exception("Unreadable settings silently replaced by defaults");}
catch(IOException) { }
MusicDeckQueueStateStore.SaveTo(queuesPath,queues);
File.WriteAllText(queuesPath,"broken queue JSON");
var restoredQueues=MusicDeckQueueStateStore.LoadFrom(queuesPath);
if(restoredQueues.Deck1.Single().Title!="Keep A" || restoredQueues.Deck2.Single().Title!="Keep B") throw new Exception("Queue recovery lost tracks");
File.WriteAllText(favouritesPath,"broken favourites JSON");
var brokenFavourites=new MusicFavourites(favouritesPath);
try {brokenFavourites.Set(new[]{Path.Combine(directory,"another.mp3")},true);throw new Exception("Corrupt favourites overwritten");}
catch(System.Text.Json.JsonException) { }
if(File.ReadAllText(favouritesPath)!="broken favourites JSON") throw new Exception("Favourites changed after failed load");
Console.WriteLine("PASS: skin changes preserve favourite and playlist files byte-for-byte; queue recovery retains both decks; unreadable favourites are not replaced.");
Console.WriteLine("PASS: every non-skin field preserved, including unknown fields; interrupted-write recovery; damaged file preserved; invalid files rejected. All tests used a temporary settings directory.");
