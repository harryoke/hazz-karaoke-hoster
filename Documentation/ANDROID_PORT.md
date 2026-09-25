# Hazz Karaoke Hoster Android v0.1

This branch starts the Android port without changing the existing Windows host.

## Architecture

- `HazzKaraokeHoster.Core` is shared unchanged.
- `HazzKaraokeHoster.Data` remains the Windows data project unchanged.
- `HazzKaraokeHoster.Data.Android` compiles the real shared SQLite repository sources for Android, excluding only the three Windows OLE DB / Access adapters.
- `HazzKaraokeHoster.Android` is a native .NET 10 Android tablet app.

## v0.1 implemented

- Landscape tablet host shell.
- Shared Hazz SQLite schema/database.
- Karaoke, Music and Music Video search using the existing FTS/search implementation.
- Active singer rotation.
- Add/save singer.
- Add selected Karaoke result to a singer.
- Rotation standing using the same `SingerQueueEntry` model: `NEXT 1 / N`, `2 / N`, etc.
- HOLD, Move Up, Move Down and Remove From Show.
- Import a copy of an existing `hazz-hoster.db` using Android's document picker.
- Windows Hazz database and Windows application are not modified.

## Important v0.1 limitation

An imported Windows database can immediately provide singers, history and searchable library metadata, but Windows drive-letter paths such as `E:\Karaoke\...` are not valid Android media locations. The next storage milestone is SAF/USB folder mapping so those indexed paths can be mapped to Android/USB content locations.

## Next platform milestones

1. Android Storage Access Framework media roots and Windows-path remapping.
2. Android audio/video playback service.
3. CD+G renderer and MP3+CDG synchronisation.
4. Secondary-display/HDMI audience output.
5. Persist/restore active show state on Android.
6. Music deck/side-list controls and transition/video surfaces.
7. Signed APK/AAB release packaging.

## Build

Install .NET 10 and the Android workload, then:

```
dotnet workload install android
dotnet build src/HazzKaraokeHoster.Android/HazzKaraokeHoster.Android.csproj -c Release
```

The Android CI workflow performs the same build and uploads the generated APK(s) as an Actions artifact.
