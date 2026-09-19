These historical test notes are retained for reference. The skins are included in v1.5; use Documentation/RELEASE_v1.5.md for current instructions.

# Console skins test — based on v1.4

This is a test build, not a published update. The v1.4 release remains available unchanged.

## Choose a skin
1. Open DISPLAY at the top of the main window.
2. Open Console skin / layout.
3. Choose Classic, Midnight, Copper or Daylight.

Classic restores the original three-column v1.4 console. Midnight places the singer workspace on the left, with two stacked music players and their playlists on the right. Copper puts both music playlists above their controls, with karaoke on the right. Daylight uses light panels and dark text, with singers on the left and two music decks alongside.

Skins rearrange the same controls. Playlist contents, singer selections, playback controls, waveforms, menus, photos, favourites, drag-and-drop and keyboard handlers are shared. Play, pause and stop retain their functional colours. The skin choice is saved with the interface settings and venue profiles. Unknown or missing skin settings use Classic.

Single Deck + Side List and Karaoke Only remain choices under SHOW. These modes intentionally hide their usual player controls; changing a skin does not change the selected mode. Search results move with the layout so music playlists remain available when searching for music.

Main interface text size remains under DISPLAY. Small displays fit the whole console to the available space; at very small resolutions this necessarily reduces the physical text size.

## Two download choices
- **Portable folder ZIP:** extract the complete ZIP to a new folder and keep its DLLs and libvlc folder with the EXE. This is the existing distribution format.
- **Optional single EXE:** put the EXE wherever you want and run it. No accompanying DLL folder or separate VLC/.NET install is needed. At startup, .NET extracts bundled runtime and VLC files into its per-user temporary cache. It can take longer on the first start. It is a single file to distribute, not a promise of zero files written to disk.

Both choices have the same features and use the normal Hazz database/settings location. They are not isolated copies of your singer database. Keep your normal backup before trying a test build, and close Hazz before switching between builds.

## Build in Visual Studio 2026
1. Install the .NET desktop development workload and .NET 10 SDK.
2. Open HazzKaraokeHoster.sln.
3. Select Release / x64 and rebuild the solution, keeping the Release project enabled.
4. The normal package is in RELEASE/HazzKaraokeHoster.
5. The additional single executable is in RELEASE/SingleFile.

BUILD-EXE.cmd builds the normal folder package. BUILD-SINGLE-EXE.cmd builds only the optional single executable. No source trimming is enabled.

## Test coverage and limits
The build validates XAML event handlers and references. The isolated WPF layout checks exercise every skin at normal and 150% text size, in normal, single-deck and karaoke-only modes, at 1920x1080, 1366x768 and 1024x768. They check control identity and button bounds through repeated skin switching. Preview images use sample playlist and singer data, not a live show.

The single-EXE package check loads its bundled VLC runtime/plugins and opens SQLite in memory without touching the show database. This does not substitute for extended playback, physical display and sound-device testing.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
