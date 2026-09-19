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


## Earlier features retained in v1.5

# Hazz Karaoke Hoster v1.4 — new features and instructions

Version 1.4 adds music handover choices, real deck waveforms, venue singer photos and favourites in search. Doctor integration remains separate.

## Build with Visual Studio 2026
1. Extract the entire ZIP to a new folder.
2. Install the .NET desktop development workload and .NET 10 SDK.
3. Open HazzKaraokeHoster.sln, allow NuGet restore, select Release / x64.
4. Choose Build > Rebuild Solution. Keep the Release project enabled.
5. Use RELEASE/HazzKaraokeHoster/Hazz Karaoke Hoster.exe. Keep all accompanying DLLs and the libvlc folder.
BUILD-EXE.cmd is the alternative publish command.

## Choose what music does when karaoke starts
1. Open SHOW > Music when karaoke starts.
2. Choose one:
   - Fade and stop — next queued track (default): existing behaviour; the interrupted music is consumed and queued music returns.
   - Fade then pause — resume same track: music fades using the crossfade duration, pauses at that point, then fades back from that position.
   - Fade then keep playing silently: the music continues advancing at zero volume. When karaoke ends, it fades back at its current position.
3. Start a music track on Deck 1 or Deck 2, then press karaoke PLAY.
4. Use karaoke STOP or PLAY MUSIC to return. The existing karaoke STOP return fade is 1.5 seconds.
The choice is saved. Changing it during a karaoke song applies to the next handover.
If a silent track ends, Hazz waits for karaoke to finish then uses queued music; it does not loop or start another track silently.
During a crossfade, the incoming deck is retained and the outgoing track is finished.
This option applies to the regular music decks. Space-bar search preview keeps its existing stop behaviour.

## Waveforms
Load/play music on either deck. An overview is generated in the background.
Click the waveform to seek. Unsupported files show a message; the position slider remains available.

## Singer photos
Right-click a singer in the main list and choose CHOOSE SINGER PHOTO.
The webcam action opens Windows Camera; take/save the picture and select it afterwards.
Photos are stored by singer name and active venue; no venue uses the default collection.
In DISPLAY > audience settings, enable Show singer photos and adjust Photo size and Fit, Fill/crop, Stretch or Center.
Photos appear in the main singer list and beside the upcoming singers on the TV. The upcoming list follows existing hide-during-karaoke behaviour.

## Search favourites
Right-click a search result to mark/unmark it as a favourite. Music search supports multiple selected rows.
Saved favourites have a yellow star in search results.

## Testing
The exact packaged source passed Release compilation with zero warnings/errors.
Automated handover logic checks passed for pause, mute, default, early return, ended-track fallback, crossfade and setting changes.
These checks use simulated music devices. Test pause/resume and silent playback with your actual sound devices before a live show.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.


---
The sections below cover the existing controls. Earlier screenshots are labelled with their original version.

# Hazz Karaoke Hoster Quick Reference

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.

## Venue singers (v0.97 test)

**Show > Venue Profiles**: SAVE CURRENT / APPLY for settings; SAVE SINGER LIST / LOAD SINGER LIST for names, queue and history. Untick history for a fresh history. START BLANK SINGER LIST clears current singers; KEEP SELECTED SINGERS retains regulars. CLEAR HISTORY ONLY retains names. RESTORE SINGER BACKUP recovers singer snapshots. Active venue singers save automatically when requested, before venue changes and at shutdown; no-venue history still saves normally. See [full instructions](VENUE_PROFILES.md).

**Show > Rotation Settings**: manual by default; explicitly enable software-managed rotation to use the selectable methods. **Sound Devices** and **Normalize Audio** have their own top buttons.

## Main sequence

1. Add singer.
2. Drag a Karaoke search result onto the singer.
3. Press Load Next Singer.
4. Press TV to confirm Display 2.
5. Press Play.

## Emergency controls

- Music Pause keeps the exact position; press Pause or Play to resume.
- Drag a music timeline to seek to an exact point.
- Fade Stop lowers karaoke for 1.5 seconds, then fades music in for 1.5 seconds.
- Hold temporarily removes a singer from automatic selection.
- Skip Once passes them once.
- File failures are marked as broken and colour-coded.

## Music videos

- Press **Music Video** beside Karaoke and Music to search only music-library video files.
- Drag selected videos to a deck, use a deck button, or press Space for quick play.
- The singer lists and every other audience overlay hide while the video is visible.

## Safe shutdown

Close the main window and answer Yes. Wait for Hazz to close before removing media drives.

## Virtual folders

1. Open **Library > Browse Library**.
2. Use **New Folder**, or select a parent and use **New Subfolder**.
3. Select tracks with Ctrl-click, Shift-click or Ctrl+A.
4. Press **Add Selected to Folder…**, choose the full path and press **Add Tracks**.
5. **Remove from Folder**, **Empty** and **Delete Selected Folder** remove links only; media files remain.
6. **Import > Import BPM Studio** imports recoverable `.GRP`/`.PLG` groups below **BPM Studio**.
7. Duplicate BPM group filenames use only the newest copy; unchanged groups are skipped on repeat import.
8. Wait for **Import complete** after the Linking, Saving and Finalizing stages.
