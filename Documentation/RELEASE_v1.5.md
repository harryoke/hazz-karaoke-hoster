# Hazz Karaoke Hoster v1.5

Version 1.5 adds a folder-only Kamikaze pool and drag-edge scrolling, and brings the newer playback, search, layout and data-protection fixes into the main release. Your existing singers, favourites, venue settings and music handover choices are retained.

## Choose the songs used by Kamikaze
1. Put the songs you want into one folder. For a five-song game, put those five songs directly in the folder.
2. In Hazz, open SHOW, then Kamikaze song source, then Choose folder only.
3. Select that folder. Hover over the menu choice or KAMIKAZE button to see its saved path.
4. Add singers normally. Press KAMIKAZE to choose for the next singer, or right-click a singer and choose KAMIKAZE FOR THIS SINGER.
5. Hazz picks one supported song at random from that folder. Press KAMIKAZE again to change the selection; it avoids the previous choice when another song is available. This is not a shuffle-bag: earlier songs may return on later picks.
6. Press karaoke PLAY when ready. The existing announcement, loading and music handover behaviour still applies.
7. To return to the usual pool, choose SHOW > Kamikaze song source > Entire karaoke library (default).

Only files directly in the chosen folder are used. Subfolders are excluded. A matching CDG and audio file count as one song. ZIP karaoke, supported video and audio files are accepted; an orphan CDG, unsupported file or unreadable ZIP is skipped. Use karaoke content in this folder: Hazz cannot determine whether a video or audio recording contains vocals. A single-song folder always selects its one song, even on repeat presses.

The folder choice is saved across restarts. No bulk import is needed: the selected song is added to the library if it is not already indexed, so ordinary singer history still works. If the folder is empty or unavailable, Hazz reports it and does not choose from the full library. Reconnect the drive or choose another folder. A supported file can still fail playback if its media content or codec is damaged; test the pool before the show.

## Drag through a long list
1. Start dragging a song or singer normally and keep the mouse button held.
2. Hold the pointer near the top of the destination list to scroll up, or near the bottom to scroll down.
3. Keep holding to continue through the list. Move into the middle to stop scrolling.
4. Release over the required position to drop. Escape cancels the drag.

This works with both music decks, the side list, singer rotation, a singer's queued songs and the virtual-folder tree. Existing move/copy rules stay the same. It scrolls existing rows; it does not turn the library browser's separate search pages.

## Choose a console skin and working mode
Open DISPLAY > Console Skin and choose Classic, Midnight, Copper or Daylight. Classic keeps the original layout. Midnight stacks music decks beside karaoke; Copper places playlists above controls; Daylight provides a light background and a different arrangement. The same controls are used in every skin.

Under SHOW, Karaoke Only hides music decks, Single Deck uses Deck 2 as a side list, and Karaoke Focus puts singers in the area normally used by Deck 2 and enlarges the karaoke preview. Focus uses Deck 1 for music. Leaving Focus keeps Single Deck enabled until you turn that option off. Your skin choice also affects where these panels sit. Host text size and automatic fitting remain available.

## Seek karaoke and correct CDG sync
Drag the karaoke progress slider to choose a position, or use the minus/plus five-second buttons. CDG graphics rebuild for the selected position. Pause remains pause; seeking does not replace the loaded song.

For CDG sync, positive values advance the graphics and negative values delay them. Use the 0.25-second buttons and RESET to return to zero. For example, plus 1 second displays the graphics for second 11 when the audio is at second 10. Check any saved adjustments made while using earlier test versions with reversed sync behaviour; saved numbers are not automatically changed.

## Track lengths and remaining show time
Karaoke search shows known track lengths and reads up to 80 uncached results in the background. Unknown lengths may remain blank. Repeat searches can use the saved length. REMAINING above the singer list adds queued requests and the current karaoke song's remaining time. Held singers are excluded. An unknown length uses a four-minute estimate and a tilde marks an estimate. This is a guide, not a guaranteed finish time; tempo changes, introductions and breaks can change the real duration.

## Search cleanup and singer history export
Search accepts apostrophes and other punctuation. Right-click a result to mark it as a favourite or remove its database entry. Remove missing results affects the current displayed results only. Database removal does not delete the media file or the singer's history. Existing yellow favourite stars remain available.

To export history, open a singer's songs/history window, type a filter if wanted, then press EXPORT CSV. Choose where to save it. The file contains the displayed history, including singer, song, artist, date, count, key, sync and file path.

## Choose the download format
The normal Windows ZIP contains the app and its runtime folders. Extract it and keep the whole folder together. The optional Single EXE ZIP contains one app EXE plus instructions; it automatically extracts its bundled runtime when launched. Both formats use the same existing Hazz data location and do not create separate singer libraries. No separate VLC installation is required.

For Visual Studio 2026, install the .NET desktop workload and .NET 10 SDK, open the solution and build Release. RELEASE/HazzKaraokeHoster contains the normal app; RELEASE/SingleFile contains the optional EXE. BUILD-EXE.cmd and BUILD-SINGLE-EXE.cmd build each format separately.

## Saved data and reliability fixes
Skin selection saves independently of audience controls. Settings retain unknown options and recovery copies; failed reads cannot overwrite them with defaults. Startup guards protect queues and venue snapshots before recovery succeeds. Newer fixes also cover Karaoke Focus music return, scrolling deck LEDs, stale search refreshes, right-click selection, ZPB catalogue names and crossfade preferences when entering Focus.

Compilation, temporary-data regression tests and layout checks pass. Actual sound devices, external TVs and multi-hour playback depend on your hardware and still require a rehearsal before a live show. Doctor integration remains a separate development project.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
