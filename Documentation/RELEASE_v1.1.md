# Hazz Karaoke Hoster v1.1

## Install or update

1. Download `Hazz_Karaoke_Hoster_v1.1_Windows_x64.zip` and select **Extract All**
   into a new folder.
2. Close any running Hazz copy, then open `Hazz Karaoke Hoster.exe` in that folder.
3. Keep the entire folder together, including the `libvlc` folder, DLLs and
   third-party notices. This release is a portable folder, not a single EXE.
4. Your normal Hazz database and settings are used automatically. The separate
   experimental LibVLC test database is not imported.

## Larger text

Open **DISPLAY > Main interface text size** and choose 100%, 110%, 125% or 150%.
The main playlists, side list, singer list, search results, menus and buttons
change immediately. Choose 100% to return to the original size. Larger text
leaves room for fewer rows; lists remain scrollable. Separate browser/dialog
windows retain their existing sizes. Audience font settings are separate.

## Windows or VLC audience video

Open **DISPLAY > Audience video engine** and choose **Windows (default)** or
**VLC (Windows fallback)**. The preference applies when the next video is loaded,
so changing it does not interrupt the song currently playing. Reload a stopped
video to compare the two engines on your laptop and external screen.

VLC is used for audience karaoke/music-video pictures only. The private preview,
audio routing and pitch controls keep their existing engines. Audience video
remains muted to prevent duplicate sound. Fullscreen positioning and audience
logo/scroller preferences are retained. Background slideshow videos retain their
existing muted looping engine. VLC is included; no separate installation is needed.

If VLC fails to open/play a file, Windows playback is attempted. Diagnostic logs
under `%LOCALAPPDATA%\Hazz Karaoke Hoster\Logs` record `LIBVLC FALLBACK`.

## CD+G colours and picture scaling

A transparency-decoding error could turn pale-grey artwork into black. v1.1
correctly reads the transparency values for all 16 colours; an all-zero packet
keeps all colours opaque. This correction is automatic.

**DISPLAY > Smooth CD+G picture** softens enlarged dot patterns. Untick it for
crisp pixels. This affects the preview and audience picture without changing
the song file. It cannot add detail absent from the low-resolution original.

Text size, engine choice and smoothing are saved between sessions and included
in venue profiles. Loading a venue profile restores its saved preferences.

## Validation and limits

Release compilation, audience-control regression tests, CD+G opacity/partial
alpha/backward-seek tests, and real-video Windows/VLC play, pause, seek,
engine switching, overlay transfer, fallback and disposal checks passed.
The reported CD+G file was rendered to verify the grey-palette correction.
Test your laptop/TV setup before a show: these checks do not establish hours-long
stability or guarantee that changing the decoder fixes every source of judder.

The source ZIP targets Visual Studio 2026/.NET 10. Build Release/x64 or run
`BUILD-EXE.cmd`; distribute the whole published folder. Third-party components
retain their own licences; see `ThirdParty/README.md`.
