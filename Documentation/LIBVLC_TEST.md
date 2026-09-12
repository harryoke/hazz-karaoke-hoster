# LibVLC test 1 — audience video experiment

This is an experimental Windows x64 build based on v1.0, not the next stable
release. It replaces the audience karaoke and music-video decoder with LibVLC.
Hardware decoding is enabled when available. The host preview, audio routing,
pitch controls, CD+G rendering and background slideshow keep their existing
engines. Installing VLC separately is not required.

## How to test

1. Download the LibVLC Test ZIP and choose **Extract All** into a new folder.
2. Keep every file and the entire `libvlc` folder together. This test is a
   portable folder, not a standalone EXE. Do not overwrite your usual Hazz copy.
3. Open **START-LIBVLC.cmd**. The header says **LibVLC TEST 1**.
4. This build starts with a separate empty library and settings under
   `%LOCALAPPDATA%\Hazz Karaoke Hoster LibVLC Test`. Your normal show database,
   venues and saved queues are not loaded or changed. Load a video directly
   with the karaoke deck's **LOAD FILE** button, or import a small test folder.
5. Open the audience display with **TV**, send it to your external screen, and
   play the video that juddered. Check picture smoothness and lyric/audio sync.
6. Try pause/resume, seeking, stop/replay, another video, and closing/reopening
   the audience display. Also test music videos and your preferred logo/scroller
   options, followed by a CD+G song.
7. Close the test app. Open **START-WINDOWS-VIDEO.cmd** and repeat the same file
   and display settings. This uses the original Windows audience decoder and
   the same isolated test settings. Run only one comparison copy at a time.
8. To return to normal shows, close the test and open your usual Hazz shortcut.

If LibVLC initialization or playback fails, the audience surface attempts
Windows playback and records **LIBVLC FALLBACK** in the test folder's `Logs`.
Successful initialization records **LIBVLC**. Include that log and your laptop
model/GPU when reporting results. Do not delete your normal database to test.

## What has been checked

The Release build compiles with zero errors and warnings. The automated WPF
smoke test uses a real local video, checks clock movement, pause, seeking,
switching between karaoke and music video, overlay transfer and repeated
window/player disposal. Hardware decoding was observed on the development PC.
This is not an hours-long soak test or proof that the laptop judder is fixed.
External-monitor placement, mixed display scaling and visual overlay appearance
need testing on the intended laptop/TV setup. LibVLC reported taskbar thumbnail
clipping warnings during automated playback; the checked operations passed.

The original audio/host preview still uses Windows playback. If that component
is the bottleneck, this audience-only experiment may not resolve it. Existing
250 ms clock-drift correction is retained for a controlled decoder comparison.

## Building in Visual Studio 2026

Open `HazzKaraokeHoster.sln`, allow NuGet restore, and build Release/x64. Use
`BUILD-EXE.cmd` to publish the portable folder under `RELEASE\HazzKaraokeHoster`.
Distribute the whole folder including native plugins, notices and launchers.
Build output alone is not the published package.

For the playback test, run:

```
dotnet run --project tests/LibVlcSmoke -c Release -- "C:\path\test.mp4"
dotnet run --project tests/LibVlcSmoke -c Release -- "C:\path\test.mp4" --windows-video
```

LibVLCSharp/WPF 3.10.1 and VideoLAN.LibVLC.Windows 3.0.23.1 are third-party
components under their own terms. See `ThirdParty/README.md` and the included
licences. Hazz's branding restrictions do not override their licence rights.
