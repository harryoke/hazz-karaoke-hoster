# Hazz Karaoke Hoster Android v0.2

Android v0.2 builds on the working v0.1 APK while keeping the Windows Hoster unchanged.

## Architecture

- `HazzKaraokeHoster.Core` remains shared.
- `HazzKaraokeHoster.Data` remains the existing Windows data project.
- `HazzKaraokeHoster.Data.Android` compiles the same Hazz SQLite repositories for Android, excluding only the Windows OLE DB/Access import adapters.
- `HazzKaraokeHoster.Android` is a native .NET 10 Android tablet app.

## v0.2 improvements

- Everything from Android v0.1 remains.
- **Persistent show state**: singer order, HOLD state and queued singer songs are saved and restored after the app is closed/reopened.
- **Windows media-root mapping**: map a Windows library prefix such as `E:\Karaoke` to a folder on Android, SD card or USB storage using Android's Storage Access Framework.
- Mappings are persisted and use the longest matching Windows prefix.
- **Real Android playback** for mapped ordinary audio files.
- **In-app video preview playback** for mapped Music Video files.
- PLAY SELECTED, PAUSE / RESUME and STOP controls.
- ROOTS screen shows the currently configured Windows→Android mappings and can clear them.
- Existing imported Hazz databases can be searched without changing the Windows database.
- Windows Hazz remains completely separate.

## How media mapping works

1. Import a copy of your Windows `hazz-hoster.db`.
2. Search for a track from the imported library.
3. Press **MAP MEDIA ROOT**.
4. Enter or accept the Windows root, for example `E:\Karaoke`.
5. Pick the matching Karaoke/Music folder on the Android device, SD card or attached USB drive.
6. Hazz stores persistent permission to that Android folder and resolves the remainder of the Windows path underneath it.

Multiple mappings are supported, so Karaoke, Music and Music Video can live on different storage devices.

## Current playback limits

- Ordinary mapped audio/video files can be played in v0.2.
- CD+G/`.zip` karaoke graphics are not yet rendered. Those tracks remain searchable/queueable, but Android CD+G audio+graphics synchronisation is the next playback milestone.
- HDMI/secondary audience output is not yet enabled.

## Next milestones

1. MP3+CDG and ZIP karaoke playback with CD+G graphics.
2. Secondary-display/USB-C/HDMI audience output.
3. Android music decks / side list and crossfade controls.
4. Venue/show profiles and fuller settings migration.
5. Signed release packaging and updater flow.

## Build

```
dotnet workload install android
dotnet build src/HazzKaraokeHoster.Android/HazzKaraokeHoster.Android.csproj -c Release
```

The Android v0.2 workflow performs the same build and uploads the APK as a GitHub Actions artifact.
