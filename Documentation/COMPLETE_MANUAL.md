# Hazz Karaoke Hoster v1.7 display and sync guide

Made For KJ/DJ's By A KJ/DJ

Version 1.7 adds video AV Sync, a complete audience preview and optional transparent CD+G backgrounds. Existing playback, singer history, venue profiles, two scrollers and Fit/Stretch controls remain available. Transparency starts off.

## Correct karaoke video timing with AV Sync
1. Load and play a karaoke video. Listen to the sound and watch the audience picture or preview.
2. Find CDG / VIDEO AV SYNC on the karaoke deck.
3. Press +0.25s when the picture or displayed lyrics are late. A positive value moves the picture ahead of the audio. For example, at audio time 10 seconds, +1 second shows video time 11 seconds.
4. Press −0.25s when the picture is early. A negative value delays the picture. At audio time 10 seconds, −1 second shows video time 9 seconds.
5. Use small steps until it looks right. The available range is −10 to +10 seconds. Sound continues normally; the control adjusts the picture.
6. Press SAVE to keep that track default. Press RESET for zero; press SAVE afterwards if you want zero to replace the saved default.

The same controls still adjust CD+G graphics. Video AV Sync works with Windows and VLC audience video, including pause, resume, seeking and the preview. Negative offsets hold the opening picture until audio reaches the required point; positions cannot run before the start or beyond the end of a video. This corrects a consistent offset, not a badly edited recording whose timing varies throughout the song. It does not adjust ordinary music-deck videos.

The track default is used when loading the file directly and is also saved to its library record when available. Existing queued singer/song sync preferences remain independent and take precedence when loading that singer's request. Adjust those in the singer's song/history window or while that singer's song is loaded. File-based defaults are matched by the original path, including the original ZIP path for zipped karaoke; moving or renaming a file changes that match. Saving does not rewrite the media file.

## Preview the whole audience screen
1. Press SHOW PREVIEW above the singers list if the preview is hidden.
2. The panel now shows AUDIENCE PREVIEW. Between songs it includes your background, singer names and photos, logo, both enabled scrollers and any Kamikaze announcement.
3. Open DISPLAY > Audience Settings and change your messages or layout. The preview follows the actual audience scene, including the same scroller positions and slideshow item.
4. Start karaoke or a music video. The preview follows playback and the audience overlay visibility rules. During karaoke, singer lists and scrollers remain hidden as configured by Hazz's normal karaoke display behaviour.
5. Press HIDE PREVIEW to recover space and release its extra video decoder. Showing it again reconnects to the current scene.

The preview works without opening a TV window, using a 16:9 preparation scene until an audience window supplies its size. When the audience window is open, the preview fits its proportions. It never sends an audience window to another display by itself. Normal TV and display-selection buttons still control where the audience sees the show.

The preview's video decoder is muted, so it cannot add a second copy of the audio. Native video is mirrored using a separate muted decoder and can differ by a few frames; this is a monitoring preview, not a frame-accurate recording. Backgrounds, photos and text use the shared audience scene. A powerful video may need more resources with preview enabled, so hide the preview if your laptop is struggling.

## Put your own background behind CD+G lyrics
1. Open DISPLAY > Audience Settings.
2. Enable Singer-view background. Choose an image, GIF or video, or choose a folder for the existing one-minute slideshow.
3. Enable Transparent CD+G background.
4. Leave Remove colour on Auto first. Hazz removes the colour identified by the CD+G memory-preset background instructions.
5. Play the CD+G song and check AUDIENCE PREVIEW. The chosen background now continues behind the remaining graphics during karaoke.
6. If Auto removes the wrong area, try Palette 0 through Palette 15 in Remove colour. These are the song's sixteen colour slots, not fixed names such as black or white. Choose the slot that removes the background while keeping the words readable.
7. Adjust Background opacity to dim your artwork towards black. Adjust Lyrics opacity separately; 100% keeps the remaining CD+G graphics fully visible.
8. Use the existing background Screen fit and GIF speed controls as needed. Karaoke Fit/Stretch still controls the CD+G picture itself.

CD+G is a coloured pixel image, not a separate lyrics layer. Any artwork or lyric pixels using the selected colour can also disappear. Lyrics opacity affects all remaining CD+G graphics, including title cards and drawings. Check each song and keep the background calm enough for the words to stay readable. This feature does not remove backgrounds from MP4 or other ordinary karaoke videos.

## Keep a CD+G adjustment for one song
1. Load the CD+G song and adjust transparency, colour slot and both opacity sliders.
2. Press SAVE FOR THIS SONG. Hazz stores that combination against the original song path. It works for a CDG/audio pair or a karaoke ZIP, without modifying either file.
3. Load the song again to recall the saved combination. The controls show the recalled settings.
4. Press USE DEFAULTS to remove that song's override and return to the general CD+G display defaults.
5. Press RESTORE ORIGINAL for an immediate opaque, full-opacity CD+G picture. If the song already has a saved override, press SAVE FOR THIS SONG afterwards to keep the restored appearance for future loads.

When no song override is active, changing these controls changes the general default. When an override is active, changes are temporary until SAVE FOR THIS SONG is pressed. Close Hazz normally to keep general settings. Venue profiles save the display settings under the existing venue save rules; an older profile without these options uses transparency off. Read the status line after saving: if storage is unavailable, Hazz leaves the show running and asks you to retry.

## Downloads and testing
The normal Windows folder ZIP and optional Single EXE ZIP contain the same features. Close your old Hazz, extract the new download and launch it. Keep every runtime folder with the normal ZIP. Both versions use your existing Hazz data location. The single EXE extracts its bundled runtime automatically. No separate VLC installation is required.

For Visual Studio 2026, install the .NET desktop development workload and .NET 10, open HazzKaraokeHoster.sln and build Release / x64. The normal package is produced in RELEASE/HazzKaraokeHoster, and the optional executable in RELEASE/SingleFile.

Validation covers compilation, CD+G alpha/keying and seek reset, saved-setting preservation, preview-only composition, four-skin layouts, Windows/VLC AV Sync, pause, seek, preview muting and fallback. Please rehearse on your own laptop, audience display and sound devices before using the update at a live show; a multi-hour hardware show test is not part of these automated checks.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.


## Retained features and instructions

# Hazz Karaoke Hoster v1.6 audience display guide

Version 1.6 adds Fit or Stretch for karaoke playback, separate rotation and message switches, and a second independent scrolling message. The existing Fit display and combined primary scroller remain the defaults. The second scroller starts switched off.

## Fill the audience screen with karaoke
1. Open DISPLAY, then Audience Settings — Backgrounds, Logo, Scroller.
2. Find Karaoke playback sizing beside the scroller switches.
3. Choose Fit to show the entire picture in its original proportions. A square or 4:3 song on a widescreen TV will have side borders.
4. Choose Stretch to fill the audience display. This changes the picture proportions, so faces and lettering may look wider or taller.
5. Play a CD+G or karaoke video and check the TV. The setting applies to both Windows and VLC karaoke video engines, including Windows fallback. It can also be changed during playback.

This setting affects the audience karaoke picture, not ordinary music videos or idle artwork; the audience preview now mirrors this sizing. The existing Screen fit control under Singer-view background still controls images, GIFs and background videos. Black borders recorded inside a video remain part of that video; Stretch cannot remove them independently. Crisp or smooth CD+G remains available.

## Show a message without revealing the singer order
1. Open DISPLAY > Audience Settings.
2. Leave Primary scroller switched on.
3. Switch off Show singer rotation.
4. Leave Show custom message switched on and type your announcement into the message box beside Primary scroller.
5. If you also want to hide the separate upcoming-singers panel, switch off Show next 4 singers at the top of this panel.

Only the announcement now scrolls. You can rearrange the singers privately. To show the order again, enable Show singer rotation. Both content switches can be on together; rotation comes first and the message follows. Turning Primary scroller off hides the entire first bar. Turning both content switches off also hides it. An empty message adds no text. When rotation is enabled but there are no singers, the bar says Singer rotation empty.

## Add a second scrolling message
1. Switch on Second message scroller.
2. Enter the second message in its box, for example DRINKS OFFERS — ASK AT THE BAR. A blank message keeps this bar hidden.
3. Choose Second font. Drag its Size slider to choose 16–120 pixels; the number beside it shows the selected size.
4. Drag the second Speed slider to choose how fast that message moves. This does not change the primary scroller speed.
5. Press SECOND COLOUR to choose this message's colour.
6. Use the second Move away from edge slider if the TV cuts off the edge of the message.

The second message works even when Primary scroller is off. Each bar has its own font, size, speed and inset. Primary rotation and message colours still use the ROTATION and VENUE MSG buttons. Existing primary text outline settings continue to apply.

## Keep the two bars apart
1. Use Position in the primary scroller controls to choose Top or Bottom.
2. The second bar automatically uses the opposite edge. Primary Bottom means second Top; primary Top means second Bottom. This also determines the second bar's edge when the primary is off.
3. Try the edge-inset sliders while looking at the audience TV. A larger number moves that bar inward.

Hazz limits the effective inset so each bar stays in its own half of the audience screen. The requested slider value is retained, but may be reduced on a short display or with large fonts to keep the bars apart. Extremely short windows can clip large text; enlarge the audience window or reduce its font size. The upcoming-singers panel keeps clearance from visible bars.

## What happens during songs and when saving
Both scrollers and the upcoming-singers panel hide during karaoke playback and the Kamikaze announcement, as the original scroller did. They return afterwards. To show enabled scrollers over ordinary music videos, tick Both scrollers under SHOW DURING MUSIC VIDEOS. Leave it off for an unobstructed music video. Permanent logo behaviour is unchanged.

Settings apply live. Press CLOSE to dismiss the panel. Close Hazz normally to retain your display choices; do not end it using Task Manager. When using venue profiles, save or update that profile to keep these choices with the venue, following the existing venue save rules. An older profile without the new fields uses Fit, enables both primary content switches, and leaves the second bar off. Existing singers, favourites and playlists are not changed by these options.

## Update or build v1.6
Download the normal Windows ZIP and extract the entire folder, or choose the optional Single EXE ZIP. Close the old Hazz before launching the new copy. Both use your existing Hazz data location. Keep all runtime folders with the normal download. The single EXE extracts its bundled runtime automatically. No separate VLC installation is needed.

To build yourself, install Visual Studio 2026 with .NET desktop development and .NET 10, open HazzKaraokeHoster.sln, select Release / x64 and build. The normal app is in RELEASE/HazzKaraokeHoster and the optional single executable is in RELEASE/SingleFile.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.


## Retained features and instructions

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

# Hazz Karaoke Hoster Complete Illustrated User Manual

Core illustrated instructions from version 0.80, with current feature guides below.

For v0.97 test, read [Venue Profiles and singer lists](VENUE_PROFILES.md), [Rotation Methods](ROTATION_METHODS.md), [Fair Rotation](FAIR_ROTATION.md), [CPU Improvements](CPU_IMPROVEMENTS.md), and [v0.95 audio and confidence features](WHATS_NEW_v0.95.md). Original manual rotation remains the default. The companion PDF/Word editions have not yet been consolidated for v1.0.

![Complete console map](Images/figure_console_overview.png)

## Visual guides

![Button colours](Images/diagram_button_colours.png)

![Music deck controls](Images/figure_music_deck_controls.png)

![Karaoke deck controls](Images/figure_karaoke_deck_controls.png)

![Singer queue controls](Images/figure_singer_queue_controls.png)

![Audience settings](Images/figure_audience_settings.png)

![Search routing](Images/diagram_search_dragdrop.png)

![Music modes](Images/diagram_music_modes.png)

![VJ routing](Images/diagram_vj_display.png)

![Smart import](Images/diagram_import_flow.png)

![Recovery](Images/diagram_recovery.png)

## Main control reference

The Word and PDF editions contain the expanded control-by-control reference, workflows, troubleshooting guide, supporting-window descriptions and Visual Studio build instructions.

### Live sequence

1. Import and test the karaoke and music libraries.
2. Open the audience display and send it to Display 2.
3. Add a singer and drag a request onto their row.
4. Press Load Next Singer and check the preview.
5. Press Play.
6. Press Fade Stop at the end so karaoke fades out and music fades in.

### Safety

Keep the host console on Display 1, keep media drives connected while Hazz is open, and shut down through the main window confirmation.

## Detailed written reference
Every screen, button, workflow and live-show function for version 0.80.

![Complete console map](Images/figure_console_overview.png)

## Visual guides

![Button colours](Images/diagram_button_colours.png)

![Music deck controls](Images/figure_music_deck_controls.png)

![Karaoke deck controls](Images/figure_karaoke_deck_controls.png)

![Singer queue controls](Images/figure_singer_queue_controls.png)

![Audience settings](Images/figure_audience_settings.png)

![Search routing](Images/diagram_search_dragdrop.png)

![Music modes](Images/diagram_music_modes.png)

![VJ routing](Images/diagram_vj_display.png)

![Smart import](Images/diagram_import_flow.png)

![Recovery](Images/diagram_recovery.png)

## Main control reference

The Word and PDF editions contain the expanded control-by-control reference, workflows, troubleshooting guide, supporting-window descriptions and Visual Studio build instructions.

### Live sequence

1. Import and test the karaoke and music libraries.
2. Open the audience display and send it to Display 2.
3. Add a singer and drag a request onto their row.
4. Press Load Next Singer and check the preview.
5. Press Play.
6. Press Fade Stop at the end so karaoke fades out and music fades in.

### Safety

Keep the host console on Display 1, keep media drives connected while Hazz is open, and shut down through the main window confirmation.

## Detailed written reference
Complete operating documentation for version 0.80.

## 1 Getting started

### What Hazz Karaoke Hoster does

Hazz Karaoke Hoster is a Windows live-show console for running a singer rotation, playing CD+G and video karaoke, and managing two background-music decks. It keeps the host controls on the main display and can place the singer output full screen on a second display.

### Before your first show

- Use a Windows 64-bit computer and connect the audience television or projector before opening Hazz.
- Keep karaoke and music files on drives that will remain connected during the show.
- Test Windows sound output, the second display, and several representative song formats before guests arrive.
- Keep a current database backup and avoid moving indexed files immediately before a show.

### Run the supplied build

Open RELEASE\HazzKaraokeHoster and double-click Hazz Karaoke Hoster.exe. The release is a self-contained single file; no separate .NET installation is required.

## 2 The live console

### Main areas

Deck 1 and Deck 2 hold background-music queues. The centre Karaoke Deck loads and controls the current singer song. The Karaoke Singers List is the running order. The Search bar switches between Karaoke and Music results. Library, Import, Display and Show menus contain setup and show-management commands.

### Button colours

- Green starts or confirms playback.
- Red stops, removes or clears.
- Yellow pauses or changes timing.
- Blue loads or navigates.
- Teal adds or saves.
- Purple runs special actions such as shuffle, fade and alternatives.
- A brighter button and glow show an active function.

### Automatic fitting

The console scales to the available host display. If space is limited, deck sections provide scrolling and the preview can be hidden. The splitters between columns and centre rows can be dragged, and the chosen layout is remembered.

## 3 Build the song libraries

### Import karaoke folders

Choose Import, then Import Karaoke Folders. Select one or more folders. Hazz scans ZIP and CDG karaoke, companion audio plus CDG pairs, and supported video karaoke. The selected folders are watched for new files while Hazz is running.

### Import music folders

Choose Import, then Import Music Folders. Select folders containing ordinary background music. Hazz keeps music separate from karaoke so Music search and the music decks do not become mixed with the singer library.

### Supported media

Audio includes MP3, WAV, WMA, M4A, AAC, FLAC, OGG, AIF and AIFF. Video includes MP4, MKV, AVI, MOV, MPEG, MPG, WMV, M4V, VOB, TS, M2TS, WEBM and DIVX. Karaoke packages include ZIP and CDG with a matching audio file.

### Rescan and browse

Use Library, Browse Library to inspect indexed songs. Use Library, Rescan Watched Folders after files were added while Hazz was closed or if Windows reported a watcher overflow.

## 4 Import from other programs

### Smart Import

Choose Import, Smart Import, then Detect from File or Detect from Application Folder. Hazz previews the detected program and sample records before importing. The source is opened read-only.

### Recognised sources

Detection covers common exports and databases from MediaMonkey, CompuHost, Lyrx, KaraFun, Siglos and PowerKaraoke, VirtualDJ, OpenKJ, Karma, BPM Studio, PCDJ DEX, MTU Hoster, SongBookDB, kJams, JustKaraoke, Sax and Dottys, TriceraSoft, Serato, Mixxx, djay Pro, Winamp, rekordbox and Apple Music or iTunes.

### Singer history import

Choose Import, Smart Import, Import Singer History. Select a CSV, TSV, TXT, JSON, XML, SQLite, MDB, ACCDB or KDB history export. Check the detected singer, artist, title, path and date mapping in the preview. Matching songs link to the Hazz library; unmatched performances remain in the singer history. Reimporting the same source replaces its earlier imported rows.

### Large imports

Leave Verify files exist off for the fastest database migration. Turn it on only when you need Hazz to check every stored path. Imports run away from the interface and can be cancelled safely.

## 5 Search for songs

### Karaoke search

Press Karaoke beside the Search box, then type part of an artist, title, manufacturer or disc ID. Drag a result onto a singer or use the available add action. Results leave the singer rotation visible.

### Music search

Press Music and enter an artist, title or folder name. Music results appear over the centre so both deck playlists remain visible. Shift-click selects a range and Ctrl-click selects individual tracks. Ctrl+A or Select All selects the complete visible result set. Drag the selection to a deck or press Add to Deck 1 or Add to Deck 2. Tracks are added in displayed order; missing files are marked as broken and skipped.

### Music Video search

Press Music Video beside Karaoke and Music to show only imported video files from the music library. Type part of the artist or title, then use Shift-click, Ctrl-click, Ctrl+A, drag and drop, or the deck buttons in the same way as Music search. Space-bar quick play also sends a selected music video to the audience display.

### Space-bar quick play

In Music search, highlight a result and press Space to play it immediately without adding it to a deck. If music is already playing, Hazz fades into the selected result. Press Space again to stop quick play. Hazz then waits for Play Music or the next karaoke song.

### Broken results

A missing or unplayable file is tagged as broken and colour-coded. The tag records the failure reason so the host can avoid retrying it during the show.

## 6 Manage singers and requests

### Add a singer

Type or choose the singer name above the Karaoke Singers List and press Add Singer. Opening the previous-singer list shows a capped recent set immediately; typing searches the full singer database in the background.

### Add songs

Drag a Karaoke search result onto the singer. Double-click the singer, or use the singer right-click menu, to open Songs and History. Songs may be reordered, removed, or adjusted for key and CD+G sync.

### Change the rotation

Drag singers, use Move Up or Move Down, or press Set As Next. The right-click menu also provides Move to Top. Remove deletes the singer from the current show after confirmation where applicable.

### Hold and skip

Hold keeps the singer visible but causes Load Next Singer to pass over them until released. Skip Once passes over that singer one time. These controls are useful for a temporary absence without losing requests.

### Past songs

Songs and History shows previous performances with last-sung date, times sung, key and sync. Search by artist or title. Double-click or drag a history item to request it again. Hazz warns about a song already performed during the current show, but the host can continue.

## 7 Play karaoke

### Normal sequence

- Press Load Next Singer to load the first eligible singer request.
- Check the singer and song shown in the Karaoke Deck.
- Press TV if the audience screen is not already full screen on Display 2.
- Press Play. Hazz records the performance in singer history when playback begins.
- At the end, Hazz advances the show state and restores background music when configured.
- To load without assigning a singer, drag a Karaoke search result or a supported karaoke file from Windows Explorer directly onto the Karaoke Deck. Hazz loads it without starting playback.

### Pause and Fade Stop

Pause resumes from the same position. Fade Stop lowers karaoke audio smoothly for 1.5 seconds, then ends the performance and fades the returning background track in over 1.5 seconds. Media errors and shutdown still stop immediately. Starting karaoke stops Music Search quick play and suspends normal background music.

### Key

Use minus, plus and Reset to change the singer key from minus 6 to plus 6 semitones. The value is saved with the request. Live pitch change depends on whether the audio format can be loaded by the pitch engine; Hazz reports when only the saved preference is available.

### CDG graphics sync

Use minus 0.25 seconds to display lyrics earlier and plus 0.25 seconds to display them later. Reset returns to the original CDG timing. The adjustment is saved for the singer request.

### Alternative and Kamikaze

Find Alternative loads another library version with the same artist and title. Kamikaze assigns a random karaoke song to the next singer and shows the configured audience message until playback starts.

## 8 Run background music

### Load a deck

Press Add Files, drag Music search results into a deck, or use Load Playlist or History. The scrolling LED shows the deck state, artist and title.

### Queue controls

Play starts the selected or scheduled track. Pause retains the exact position; press Pause again or Play to resume. Drag the timeline slider to seek to an exact point, including while paused. Stop and Shuffle affect that deck. Remove Selected removes unplayed entries without deleting files. Delete performs the same action when the list has focus. The current playing entry is protected.

### Save and reload

Save Playlist stores the remaining unplayed order under a name. Reusing a name replaces that Hazz playlist. Music History and Lists can load saved, imported or daily history into either deck.

### Crossfade

When Auto Crossfade is enabled, Hazz prepares the other deck and fades near the end of the active track. The Time slider controls the fade length. Fade Now starts the transition immediately. Play Music starts the next scheduled background track after karaoke.

### Single Deck and Side List

Choose Show, Single Deck + Side List Mode to use Deck 1 as the only player. The complete Deck 2 player is removed and replaced by Add Files, Load List, Save List, Select All, Move Up, Move Down, Send to Deck 1, Shuffle List, Remove Selected and Clear List. Add search results, files or saved playlists to the side list, then drag tracks into Deck 1 or use Send to Deck 1. Deck 1 advances automatically. The mode and both lists are restored after restart.

### VJ music videos

Choose Import, Import Music / Video Folders to add video folders. When Deck 1 or Deck 2 plays a video file, Hazz mirrors the picture full-screen to the singer display while keeping that audience copy muted. Play, pause, resume, seek and stop remain synchronized. Karaoke automatically takes priority, and the music video returns when interval music resumes.

By default, music videos hide the singer lists, scrollers, background media and logo. In Audience Settings, SHOW DURING MUSIC VIDEOS lets you keep the logo, both enabled scrollers, next singers or Kamikaze message visible. Background media stays hidden. The usual audience display returns when the video ends.

## 9 Audience display

### Open safely

Use Display, Open Audience Display. Choose Send Full Screen To and select the intended display, or use the TV button for one-click full screen on Display 2. If Display 2 is missing, TV warns and does not place the audience output on Display 1.

### Windowed mode

Use Windowed, Move and Resize while setting up. Move the audience window to the projector, check overscan and scaling, then select full screen.

### Next singers

Display settings can show the next four singers and optionally their songs. Choose font, size, position and colours. Hazz hides next-singer and rotation information automatically during karaoke playback.

### Scroller

Enable Primary scroller, then choose Show singer rotation, Show custom message, or both. Choose its font, size, speed, colour, position and inset. Enable Second message scroller for an independent message at the opposite edge; use its separate styling controls. See the v1.6 instructions at the start of this guide.

### Backgrounds

Choose one image, GIF or muted video, or choose a folder for a slideshow. Every folder item receives a one-minute slot; video loops silently within its slot. GIF speed is adjustable from 0.25 to 4 times. Fit shows the whole picture, Fill crops edges, Stretch fills the display, and Center keeps original size.

### Logo

Choose a transparent PNG or other supported image as a persistent audience logo. Set its position and width. It remains above CDG and video karaoke when enabled.

## 10 Show safety and recovery

### New Show

Choose Show, New Show to clear the current rotation and begin a new duplicate-song window. Confirm the prompt before the list is cleared.

### Karaoke Only Mode

Choose Show, Karaoke Only Mode to hide both music decks and expand the singer workspace. Enabling this mode safely stops hidden music. Turning it off restores the deck queues.

### Recovery

Hazz saves the live singer rotation and unplayed music queues. If the previous session ended unexpectedly, Hazz can offer to restore the last show. A fresh checkpoint is written regularly during long shows.

### Closing

Closing the main window asks for confirmation. Choose No if the close was accidental. Normal shutdown releases media resources and records a clean end state.

## 11 Backups and data

### Database backup

Choose Library, Database, Backup Database and save the copy to another drive. Back up before a large import and before making major file-location changes.

### Data locations

The main database is stored under %LOCALAPPDATA%\Hazz Karaoke Hoster\hazz-hoster.db. Layout, queue recovery, broken-media information, diagnostics and temporary extracted ZIP content are stored under the same application area.

### Keep sources safe

Third-party imports are read-only. Hazz writes imported data to its own SQLite database. Do not delete the original program data until you have checked searches, paths and history in Hazz and kept a backup.

## 12 Troubleshooting

### A file is missing

Reconnect its drive or restore the file to the indexed path. The red broken-file tag protects the show from repeated attempts. Rescan the watched folder after correcting the file location.

### Video or audio will not play

Test the file in Windows Media Player and install a suitable Windows codec if required. Try another file of the same format. Hazz records playback failure details in its broken-media registry and diagnostic log.

### No audience picture

Open Display and confirm the audience window exists. Verify Windows Extended desktop mode, then explicitly send full screen to Display 2. Check the projector input and cable.

### Lyrics are early or late

Use the CDG sync buttons in 0.25-second steps. Use Key controls only for pitch; they do not alter CDG timing.

### The interface looks cramped

Maximise Hazz, hide the karaoke preview, drag the splitters, or enable Karaoke Only Mode. The interface scales and scrolls its control areas on smaller screens.

### An import is slow

For very large third-party catalogues, leave file verification off and allow the database import to finish. MediaMonkey and BPM Studio use optimised bulk paths. During a large BPM import, the progress window separately reports song indexing, virtual-folder linking, transaction commit and database finalisation. Cancel only from the displayed import control.

## 13 Visual Studio 2026 build

### Requirements

Install Visual Studio 2026 with .NET Desktop Development and the .NET 10 SDK. Open HazzKaraokeHoster.sln, select Release and x64, then choose Rebuild Solution.

### Release output

A successful Release x64 build creates RELEASE\HazzKaraokeHoster\Hazz Karaoke Hoster.exe. This is the self-contained single executable. BUILD-EXE.cmd produces the same publish output from the command line.

### If no release appears

Confirm Release and x64 are selected, restore NuGet packages, close any running copy of the EXE, and rebuild. Review the first actual compiler or publish error rather than the final MSB3073 wrapper message.

## 14 Live show checklist

### Before doors open

- Connect and test Display 2.
- Select the correct Windows audio output.
- Open a karaoke ZIP, a CDG pair and a video karaoke file.
- Load and play one track on each music deck.
- Check microphone and mixer levels outside Hazz.
- Confirm the database backup date.
- Set the audience background, logo and scroller.
- Start a New Show only after the correct rotation is clear.

### During the show

- Load and verify the next singer before pressing Play.
- Use Hold or Skip Once when someone is away.
- Avoid disconnecting library drives.
- Use coloured broken-file warnings instead of repeatedly retrying a failed track.
- Let Hazz complete normal shutdown after the final song.

## Detailed virtual-folder instructions

Virtual folders let you organise tracks into named collections such as **80s**, **Rock**, **Jingles**, **Floor Fillers** or **Requests**. They contain links to songs already indexed by Hazz. Your audio, video, ZIP and CDG files remain in their original Windows folders.

![Virtual folder controls](Images/figure_virtual_folders.png)

## Open the Library Browser

1. Choose **Library > Browse Library**.
2. The browser opens maximised. The **Virtual Folders** panel is on the left.
3. Select **All Library Tracks** whenever you want to return to the full library.
4. Choose **Karaoke** or **Music**, then search or sort the list as needed.

## Create a folder

1. Press **New Folder**.
2. Enter a name, for example `80s`.
3. Press **Create**. The new folder appears in the left tree.

## Create a nested folder

1. Select the parent folder, for example **80s**.
2. Press **New Subfolder**.
3. Enter a name such as `Rock`.
4. The new path is **80s / Rock**.

A parent shows tracks linked directly to that parent. It does not automatically combine all tracks from its children. Select the child folder to see the child's tracks.

## Add one or several tracks

1. Select **All Library Tracks** or another source folder.
2. Choose the **Karaoke** or **Music** tab.
3. Select tracks:
   - click once for one track;
   - hold **Ctrl** and click to select separate tracks;
   - click the first track, hold **Shift**, and click the last track for a continuous range;
   - press **Ctrl+A** to select every visible result on the current page.
4. Press **Add Selected to Folder…**.
5. In the destination picker, select the full folder path and press **Add Tracks**.

You can also drag the selected rows directly onto a folder in the left tree. Adding the same song to the same folder again is harmless; Hazz keeps one link.

## Put one track in several folders

Repeat **Add Selected to Folder…** for each destination. A track can appear in **80s / Rock**, **Party / Floor Fillers**, and another folder at the same time. All entries point to the same library song and physical file.

## Use a folder during a show

1. Select the folder in the left tree.
2. Use the search box to narrow that folder.
3. In **Music**, add selected tracks to Deck 1, Deck 2 or the Single Deck side list.
4. In **Karaoke**, add the selected song to the highlighted singer.
5. Select **All Library Tracks** to leave the folder and browse everything again.

## Rename, empty, remove and delete

- **Rename** changes the selected folder name. Its tracks and children stay linked.
- **Remove from Folder** removes only the selected track links from the open folder.
- **Empty** removes all direct track links from the selected folder. Its subfolders remain.
- **Delete Selected Folder** removes the selected folder, its subfolders and their virtual links.

None of these actions deletes a song from the Hazz library or removes a file from disk.

## Import folders from BPM Studio

1. Close BPM Studio.
2. In Hazz choose **Import > Import BPM Studio**.
3. Select the BPM Studio data or archive folder.
4. Check the preview counts and confirm the read-only fast import.
5. Hazz imports BPM playlists and daily history and reads recoverable `.GRP` and `.PLG` archive-group files.
6. Open **Library > Browse Library** and expand **BPM Studio**.

BPM archive filenames become Hazz folder names. Physical directories below the selected BPM folder become nested Hazz folders. Track files stay where they are. If a proprietary group contains no recoverable media paths, Hazz reports it as unreadable or unsupported at the end instead of inventing links.

If the selected source contains several `.GRP` or `.PLG` files with the same filename, Hazz imports only the newest modified copy. If their dates match, the larger copy wins. Repeat imports skip unchanged group files and re-read changed files. Large libraries are indexed and linked in batches, so the progress window remains responsive through the final database stages.

## Import folders from VirtualDJ and other software

1. Close the other DJ or karaoke program so its files are stable.
2. Choose **Import > Smart Import > Detect from Application Folder**.
3. Select the program's main data/export folder. For VirtualDJ, select its home folder containing `database.xml` and **MyLists** or **Playlists**.
4. Check the detected program and sample tracks, then start the read-only import.
5. Open **Library > Browse Library** and expand the folder named after the detected application.

Supported organisation includes VirtualDJ 2024 **My Lists** (`.vdjfolder` or XML), older VirtualDJ M3U/PLS playlist trees, Rekordbox XML playlist folders, and nested M3U, M3U8, PLS, XSPF or WPL exports from other programs. Each list becomes a Hazz virtual folder; directories around the list become parent folders. Unsupported proprietary crates are reported and left untouched.

## Examples

- **80s / Rock** — decade first, then genre.
- **Jingles / Station IDs** — show elements separated from background music.
- **Party / Floor Fillers** — reliable dance choices for quick loading.
- **BPM Studio / FileArchive / 80s** — example of an imported BPM archive group.

## Troubleshooting

- **“Choose a virtual folder first.”** Use **Add Selected to Folder…**, then select the destination in the picker. If none exists, create one first.
- **The folder looks empty.** Clear the search, check Karaoke versus Music, and confirm you selected the correct parent or child.
- **The count and visible rows differ.** The folder count covers direct links; the active Karaoke/Music filter and search can show fewer rows.
- **A button is clipped.** Maximise the Library Browser. v0.80 adjusts the folder panel to the available width.
- **BPM import appears to pause near completion.** Read the phase above the progress bar. Hazz now reports indexing, virtual-folder linking, transaction commit and database finalisation separately. Do not start a second import while the first is finishing.
- **BPM folder missing.** Confirm the selected location contains `.GRP` or `.PLG` files and read the completion report.
- **VirtualDJ folders missing.** Select the whole VirtualDJ home folder rather than only `database.xml`, so Hazz can also see **MyLists**, **Playlists** and **Folders**.
- **A file is missing or broken.** Virtual folders do not copy media. Reconnect the original drive or restore the indexed file path.


Hazz Karaoke Hoster v0.90

Audience Display Settings now includes SHOW DURING MUSIC VIDEOS with independent options for:
- Permanent logo
- Both scrollers
- Next singers
- Kamikaze message

Check an option to retain that overlay during music videos. Its normal display settings must also be enabled. Leave all options unchecked for an unobstructed video, matching v0.80 behaviour. Changes apply live and are saved for the next launch. Background artwork remains hidden behind music videos.

All v0.80 features and fixes remain included.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.