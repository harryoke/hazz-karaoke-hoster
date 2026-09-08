# Hazz Karaoke Hoster v0.80 — Visual Studio 2026

## v0.80 — Reliable large-library migration

This release brings together the scalable previous-singer picker, professional steel interface,
audience slideshow and VJ backgrounds, dependable live-show playback, virtual folders, and
read-only migration from major karaoke and DJ applications.

BPM Studio imports now skip unchanged group files, select only the newest copy when duplicate
`.GRP` or `.PLG` filenames exist, batch song search indexing and virtual-folder links, and show
the real final database stages instead of appearing frozen at 98% or 99%.

See CHANGES_v0.80.txt.

## v0.17.28 — Other-software virtual folders

Smart Import now converts VirtualDJ My Lists and legacy playlists, Rekordbox XML playlist trees,
and nested M3U/M3U8/PLS/XSPF/WPL exports into Hazz virtual folders. Imported tracks remain in
their original locations, source data stays read-only, and repeat imports do not duplicate links.
SQLite query-planner statistics are also refreshed safely once per application run for long-term use.

See CHANGES_v0.17.28.txt.

## v0.17.27 — Virtual-folder display scaling correction

The Library Browser now opens maximized, adapts its folder-panel width on smaller displays, and
uses full-width rows for long folder actions so every button remains visible at Windows scaling.

See CHANGES_v0.17.27.txt.

- BPM Studio `.GRP` and `.PLG` archive groups now import as nested Hazz virtual folders below **BPM Studio**. Source archives and media remain read-only and unmoved.
- Repeat BPM imports skip unchanged `.GRP`/`.PLG` files using a path/size/modified-time cache, while changed group files are re-read.
- Large imports batch full-text search indexing, keeping the final indexing stage responsive and cancellable.
- BPM virtual-folder links are saved in 5,000-track batches, and duplicate `.GRP`/`.PLG` filenames use only the newest modified copy.
- Complete Manual, Dummy's Guide, Quick Reference and Virtual Folders Guide now contain detailed folder workflows and troubleshooting.

## v0.17.26 — Multi-track virtual-folder correction

Select several tracks, press **ADD SELECTED TO FOLDER**, and choose the destination from the new
folder picker. Nested destinations show their full path, and the complete library remains visible.

See CHANGES_v0.17.26.txt.

## v0.17.25 — Library Browser contrast correction

Selected virtual folders, the sort dropdown and the Descending control now remain clear and
readable against the dark Library Browser interface.

See CHANGES_v0.17.25.txt.

## v0.17.24 — BPM Studio-style virtual folders

The Library Browser now supports persistent named folders and subfolders such as 80s, Rock
and Jingles. Drag or add tracks into several categories without moving or copying the media files;
then search, sort and load those folder contents with the normal Hazz controls.

See CHANGES_v0.17.24.txt and Documentation/VIRTUAL_FOLDERS_GUIDE.md.

## v0.17.23 — Singer entry correction

The singer-name and optional-song fields are now permanently labelled. ADD SINGER also accepts
a singer name typed into the formerly unlabelled wide field when the singer-name field is empty,
and it reports success or any database error visibly.

See CHANGES_v0.17.23.txt.

Professional Windows live-show console for singer rotation, karaoke playback, interval music and a dedicated audience display.

- [Product website](https://harryoke.github.io/hazz-karaoke-hoster/)
- [Download the latest release](https://github.com/harryoke/hazz-karaoke-hoster/releases/latest)
- [Dummy's Guide](https://harryoke.github.io/hazz-karaoke-hoster/quick-start.html)
- [Complete Manual](https://harryoke.github.io/hazz-karaoke-hoster/manual.html)

The downloadable Complete Manual is a 21-page illustrated guide with labelled control screenshots, workflow diagrams and a reference for every menu, button and supporting window.

## v0.17.22 — Dedicated side list and precise music controls

- Single Deck mode now completely removes the Deck 2 player, LED, transport, timeline and volume controls.
- A dedicated side-list toolbar replaces them with Add, Load, Save, Select All, Move Up, Move Down, Send to Deck 1, Shuffle, Remove and Clear actions.
- Pause retains the current music track and exact position. Press Pause or Play to resume without restarting or consuming the track.
- Each active music deck has a draggable seek slider with millisecond position feedback.
- Karaoke Fade Stop now uses a smooth 1.5-second fade down followed by a separate 1.5-second music fade in.
- Karaoke search results and supported files from Windows Explorer can be dropped directly onto the Karaoke Deck to load them without starting playback.
- VJ mode automatically mirrors video files played by Music Deck 1 or 2 to the singer display, muted and synchronized with play, pause, seek and stop.
- The dedicated **Music Video** search button shows only video files from the music library. Singer lists, scrollers and other overlays automatically disappear while the video is on the audience display.
- Use **Import Music / Video Folders…** to add folders containing music videos.

See CHANGES_v0.17.22.txt.

## v0.17.21 — Flexible music workflow and Fade Stop

- Music search now supports Shift-click range selection, Ctrl-click individual selection and Ctrl+A.
- Add to Deck 1, Add to Deck 2 and drag-and-drop carry every selected track in visible result order.
- A Select All button makes it quick to queue the complete result set for a folder or other search.
- Batch queueing skips missing files, tags them as broken and reports the number skipped.
- Karaoke Fade Stop lowers both normal and pitch-shifted audio smoothly before ending the performance and resuming music.
- Optional Single Deck + Side List mode keeps Deck 1 as the music player and turns Deck 2 into a persistent staging list.
- Tracks can be selected and dragged from the side list into Deck 1; Deck 1 then advances through its queue automatically.

See CHANGES_v0.17.21.txt.

## v0.17.20 — Function colours and singer-history migration

- Buttons now use clear function colours: play green, stop/remove red, pause/timing amber, load/navigation blue, save/add teal and special actions purple.
- Hover, press and active playback states brighten the button's own base colour.
- Added a dedicated read-only Singer History Import for CSV, TSV, JSON, XML, SQLite, MDB, ACCDB and KDB exports from other karaoke programs.
- History import previews detected columns and sample performances, preserves dates/key/times-sung data, matches Hazz library songs where possible and retains unmatched history.

See CHANGES_v0.17.20.txt.

## v0.17.19 — Readable steel dropdowns

- Replaced the Windows white ComboBox rendering with a consistent dark steel control.
- Selected values use bright text on a dark background.
- Open dropdown choices use dark rows with clear blue hover and selection highlights.
- Editable singer search retains a visible caret and high-contrast typed text.

See CHANGES_v0.17.19.txt.

## v0.17.18 — Audience screen fitting and accessible music search

- Singer-display backgrounds can Fit, Fill with cropping, Stretch to the full screen, or remain centred at their original size.
- The selected screen-fit mode applies to still images, animated GIFs and slideshow background videos.
- The singer-rotation scroller can be placed at the top or bottom of the audience display.
- Next-singer information automatically leaves room for the scroller at its chosen edge.
- Music search results now open over the centre workspace, leaving both Deck 1 and Deck 2 playlists visible for drag-and-drop.

See CHANGES_v0.17.18.txt.

## v0.17.17 — Long-show stability and performance

- Isolates recurring playback, automation and audience-display timer faults so one bad update cannot end a show.
- Saves a fresh live-show and music-queue recovery checkpoint every two minutes.
- Releases all Windows media resources explicitly when Hazz and the audience display close.
- Adds SQLite busy handling and WAL checkpoint tuning for concurrent searches, imports and playback history.
- Bounds automatic library-watch and slideshow work to protect memory during very large folder changes.
- Caps diagnostic log size and removes logs older than 21 days.

See CHANGES_v0.17.17.txt.

## v0.17.16 — Steel console and scrolling deck displays

- Rebuilt the host interface around a professional dark steel and brushed-metal theme.
- Buttons illuminate on hover, press and active playback states.
- Deck 1 and Deck 2 now have green scrolling LED displays showing playback state, artist and song title.
- The experimental online video search and its browser dependency have been removed completely.

See CHANGES_v0.17.16.txt.

## v0.17.10 — Fast MediaMonkey import

MediaMonkey imports now reuse prepared SQLite commands and load any legacy bad-path
records once instead of querying the Hazz database again for every song. Progress
updates are batched. A complete read-only import of the user's real 649 MB MM5.DB
processed 167,920 songs in 25.1 seconds (6,686 rows/second) with zero errors.

See CHANGES_v0.17.10.txt.

## v0.17.9 — MediaMonkey drive paths and one-EXE Visual Studio release

MediaMonkey `SongPath` values such as `:\music\...` are now combined with the
matching `Medias.DriveLetter`, producing the real path such as `E:\music\...`.
MediaMonkey's private `IUNICODE` collation is registered read-only for SELECTs,
while the private `mm` search tokenizer remains excluded.

Visual Studio **Release | x64 > Rebuild Solution** now builds a separate release
packaging project after the application and leaves exactly one self-contained file:
`RELEASE\HazzKaraokeHoster\Hazz Karaoke Hoster.exe`.

See CHANGES_v0.17.9.txt and BUILD-VISUAL-STUDIO.txt.

## v0.17.8 — Smart Import WPF thread correction

Smart Import now captures preview choices on the WPF UI thread before starting the
background database import. Progress updates are explicitly dispatched to the UI.
This fixes “The calling thread cannot access this object because a different thread
owns it” after confirming a MediaMonkey import.

See CHANGES_v0.17.8.txt.

## v0.17.7 — Visual Studio Build Solution release output

With **Release | x64** selected, Visual Studio **Build Solution** now creates the
complete runnable folder `RELEASE\HazzKaraokeHoster` automatically. This uses a
normal MSBuild copy after compilation and does not start another build or publish
process. The output is self-contained and includes the .NET runtime.

See CHANGES_v0.17.7.txt and BUILD-VISUAL-STUDIO.txt.

## v0.17.6 — MediaMonkey tokenizer and Visual Studio build correction

Smart Import now ignores MediaMonkey's private `mm` full-text search index and reads
the normal song table directly. The solution has also been validated using Visual
Studio 2026 MSBuild; **Build Solution** performs a normal application build and the
included publish profile or BUILD-EXE.cmd creates the self-contained EXE.

See CHANGES_v0.17.6.txt for validation details.

## v0.17.5 — Smart Import

Choose **IMPORT > SMART IMPORT** and select either an export/database file or an
application data folder. Hazz detects the likely source, previews the records and
imports through read-only adapters for SQLite, Access/KDB, XML, JSON, CSV/TSV,
M3U/M3U8, PLS, LST, WPL, XSPF and ASX.

Recognised application signatures include MediaMonkey, CompuHost, Lyrx, KaraFun,
Siglos/PowerKaraoke, VirtualDJ, OpenKJ, Karma, BPM Studio, PCDJ DEX, MTU Hoster,
SongBookDB, kJams, JustKaraoke, Sax & Dottys, TriceraSoft, Serato, Mixxx, djay Pro,
Winamp, rekordbox and Apple Music/iTunes. See CHANGES_v0.17.5.txt for scope and
validation details.

## v0.17.4 — Animated GIF speed, automatic console fit and broken-file tags

See CHANGES_v0.17.4.txt for controls and validation.

## v0.17.3 — Fast singer lookup and image/video backgrounds

See CHANGES_v0.17.3.txt for controls, supported formats and validation.

## v0.17.2 — Music search SPACE quick-play + shutdown confirmation

- In MUSIC search, highlight a result and press SPACE to immediately quick-play it.
- If music is already playing, Hazz crossfades away from it and into the highlighted search result.
- Quick-play does not add the highlighted result to Deck 1 or Deck 2.
- Press SPACE again while quick-play is active to stop all music and leave Hazz idle.
- After stopping, Hazz waits for NEXT KARAOKE SONG or PLAY MUSIC; it does not automatically resume music.
- Starting karaoke stops any active quick-play audio before karaoke begins.
- PLAY MUSIC stops any active quick-play and starts the next queued deck music normally.
- Closing the main Hazz window now asks for explicit Yes/No confirmation before shutdown.
- Choosing No cancels shutdown and leaves the live show running.

# Hazz Karaoke Hoster v0.17.1 — Visual Studio 2026 test build

## v0.17.1 — One-click TV / Display 2

- Added a dedicated **TV** button directly on the **Karaoke Deck**.
- Pressing **TV** immediately sends the singer/audience output **fullscreen to Display 2**, bypassing the DISPLAY menu.
- If Display 2 is not connected, Hazz shows a warning and deliberately does **not** fall back to Display 1, protecting the host controls during a live show.
- The existing DISPLAY menu remains available for manual display selection, windowed operation and audience settings.
- Uses the same native fullscreen placement/DPI-safe AudienceWindow path as the existing display controls.

# Hazz Karaoke Hoster v0.17.0 — Visual Studio 2026 test build

## v0.17.0 — Live show safety / singer controls

- Added **HOLD / RELEASE HOLD** and **SKIP ONCE** singer controls. Held singers remain visible to the host but are skipped by Load Next Singer and the audience upcoming rotation.
- Added a full singer **right-click menu**: Add Song/Search, Songs & History, Hold, Skip Once, Set as Next, Move Up/Down/Top, Kamikaze for this singer, Clear Songs, Edit Name/Notes and Remove from Show.
- Added **duplicate request warnings** for songs already performed since the current show began, including singer name and approximate time since the previous performance. The host can still queue the duplicate. History re-queue is covered too.
- Added automatic **live show recovery** for singer rotation and queued songs. Hazz distinguishes an interrupted autosave from a normal shutdown and offers **RESTORE LAST SHOW** on startup.
- Recovery preserves singer order, HOLD state, song paths/IDs, key and CD+G sync. Existing Deck 1/2 queue persistence and audience-settings persistence continue alongside it.
- **NEW SHOW** clears the prior recovery state and begins a fresh duplicate-request time window.
- Added singer-history date indexes so duplicate checks remain responsive with large Karma/Hazz histories.

# Hazz Karaoke Hoster v0.16.6 — Visual Studio 2026 test build

## v0.16.6 — Music deck playlist editing

- Added **REMOVE SELECTED** to both Deck 1 and Deck 2 playlist panels.
- Select one or multiple queue rows (Ctrl/Shift selection) and remove them from that deck without deleting the underlying music files, library records or play history.
- The **Delete** key performs the same action when a deck playlist has keyboard focus.
- The currently playing yellow/NOW PLAYING row is protected; other selected queued tracks are still removed. Stop/finish the playing track to consume it normally.
- Remaining rows renumber immediately and the automatic unplayed-queue persistence is updated, so removed tracks do not return after restarting Hazz.
- If a removed track was the background-music resume target during karaoke, Hazz automatically selects the next valid queued track.
- Retains v0.16.5 **SAVE PLAYLIST** on both decks.
- The bottom deck controls use a wrapping layout so SAVE / REMOVE / LOAD remain reachable on narrower laptop screens.

# Hazz Karaoke Hoster v0.16.5 — Visual Studio 2026 test build

## v0.16.5 — Save Playlist buttons

- Added **SAVE PLAYLIST** beside the load button on both Music Deck 1 and Music Deck 2.
- Prompts for a playlist name and saves the deck's remaining unplayed queue into Hazz's SQLite playlist archive.
- Preserves track order, Song ID where known, file path, artist and title.
- Reusing the same Hazz saved-playlist name updates/replaces that saved playlist rather than stacking duplicates.
- The already-started/currently-playing track is excluded, matching Hazz's unplayed-queue persistence rule.
- Saved playlists appear in **Music Playlists & History** as source **Hazz Saved** and can be loaded into either deck.
- If the archive window is already open it refreshes immediately after a save.
- Retains v0.16.4 scroller/font persistence and automatic unplayed-deck queue restore.

# Hazz Karaoke Hoster v0.16.4 — Visual Studio 2026 test build

## v0.16.4 — Queue persistence + display-settings workflow

- Deck 1 and Deck 2 now automatically persist their **remaining unplayed queues** under `%LOCALAPPDATA%\Hazz Karaoke Hoster\music-deck-queues.json`.
- Queue order, Artist, Title, library Song ID and known duration are restored on the next launch.
- A track that has actually started playing is deliberately not restored as unplayed, even if Hazz is closed mid-track/crossfade.
- Queue state is autosaved shortly after playlist changes as well as during normal shutdown.
- Audience/Singer Display Settings no longer disappears after changing a ComboBox, colour, font, logo or image setting.
- Settings remain open until **CLOSE** is pressed or the user clicks elsewhere in the main Hazz window.
- Added an explicit CLOSE button and live-settings note to the settings panel.
- Retains v0.16.1 BPM Studio Daily History, laptop scaling, singer-history search/drag fixes and all existing playback/import features.

# Hazz Karaoke Hoster v0.16.1 — Visual Studio 2026 test build


## v0.16.1 — BPM Studio Daily History

- Imports BPM Studio's dated daily played-song lists even when they are stored in the normal Lists area.
- One logical daily history is kept per calendar date; repeated archive/snapshot copies are collapsed before import.
- Daily history is visible both as individual Music History rows and as a loadable `BPM Daily History — YYYY-MM-DD` list.
- Original BPM play order is preserved. Genuine repeat plays of the same song on the same day are preserved.
- Daily history lists are shown first in Music Playlists & History, newest day first.

# Hazz Karaoke Hoster v0.16.1

## Laptop / responsive host UI
- Main window now restores against the host monitor WORK AREA rather than the complete multi-monitor virtual desktop.
- Prevents a saved 1600x920 host window being restored cropped on a smaller laptop while Display 2 is connected.
- Reduced safe minimum host size to 900x600.
- More compact header/logo/search spacing while retaining readable text.
- Deck 1, Karaoke Deck and Deck 2 control areas now have internal vertical scrolling when the window is short; transport/key/sync controls remain reachable instead of being clipped.
- Fixed-height deck/preview rows automatically shrink when the host window is reduced.
- Main music columns can shrink further while the centre singer workspace remains usable.
- Search results now stretch to the available host area instead of using a fixed 610px height.
- Audience settings popup reduced to fit typical laptop work areas and retains its own scrollbar.

## Singer history
- Added instant Artist/Title search box to Singer History (e.g. type Elvis).
- History query is database-backed and returns aggregated Times Sung / Last Sung rows.
- CLEAR button restores the complete history.
- Fixed History drag-and-drop: history uses COPY while active-song reordering uses MOVE, matching WPF drag/drop semantics.
- Double-click requeue remains available.

All v0.15.5 playback, Karma/BPM import, audience display, Kamikaze, database and standalone EXE functionality is retained.

# Hazz Karaoke Hoster v0.15.5 — Visual Studio 2026 test build

**v0.15.5 FAST BPM Studio import**

This revision addresses BPM Studio imports that could take hours on large archives.

- BPM import now imports the actual playlist/history list files (`.lst`, `.m3u`, `.m3u8`, `.pls`) in fast mode.
- `.grp` / `.plg` archive-group containers are detected but skipped during the fast playlist/history migration because they can be huge and often repeat tracks already present in the lists.
- The importer no longer calls `File.Exists` / `FileInfo` for every track reference.
- Referenced music paths are de-duplicated in a temporary SQLite table and indexed into Hazz once per unique path using a database-native bulk insert.
- Playlist/history insert commands are prepared once and reused instead of creating a new SQLite command for every row.
- Re-import cleanup is performed once for the selected BPM root instead of issuing several DELETE commands for every source file.
- Progress updates are throttled to every 500 track rows.
- Missing/moved paths remain visible in imported lists; use Hazz's normal Music Library scan/rescan later if you want full on-disk verification and metadata.
- The original BPM Studio files remain read-only.
- All v0.15.4 Karma singer-history / Times Sung fixes are retained.

# Hazz Karaoke Hoster v0.15.4 — Visual Studio 2026 test build

**v0.15.4 Karma history + BPM duplicate repair**

- Karma singer import now resolves relational Singer IDs and Song/Track IDs so previously sung songs are imported, not only singer names.
- Karma's per-song **Times Sung** count is preserved and displayed in Singer History.
- Singer History is grouped to one song row with **Last Sung / Times Sung** and supports drag/double-click re-queue into Active Songs.
- BPM Studio repeated copies of the same dated playlist/history are collapsed during import and refreshed idempotently.
- Source Karma/BPM databases remain read-only.

# Hazz Karaoke Hoster v0.15.3 — Visual Studio 2026 test build

**v0.15.3 BPM import responsiveness/progress update**

- BPM Studio import work now runs on a worker thread instead of continuing on the WPF UI thread after the preview confirmation.
- A visible **Importing BPM Studio** progress window appears immediately after you confirm the import.
- The window shows current phase/file, percentage, files processed/total, track items processed, music files linked/indexed, and skipped/unreadable list count.
- Initial candidate discovery uses an indeterminate bar; once the total number of BPM list/archive files is known it becomes a real percentage bar.
- **CANCEL** safely cancels the import and the active SQLite transaction is not committed.
- Progress updates are throttled during large playlists (roughly every 100 track items) so the progress UI itself does not become a new performance problem.
- Existing v0.15.2 monitor/fullscreen positioning fix and all v0.15.1/v0.15 features remain included.

# Hazz Karaoke Hoster v0.15.1

**v0.15.1 change:** singer history is recorded at PLAY time, so the song appears in history immediately while the singer is performing.

# Hazz Karaoke Hoster v0.15 — Visual Studio 2026 test build

Windows x64 / WPF / .NET 10 source for Visual Studio 2026.

## v0.15 focus — Karaoke Only Mode

Hazz can now be used either as the full **Karaoke + Music** hoster or as a dedicated **Karaoke Only** hoster from the same executable.

### Karaoke Only Mode
- Open **SHOW → Karaoke Only Mode (Hide Music Decks)**.
- Deck 1, Deck 2 and both music playlists disappear from the live host GUI.
- The karaoke workspace expands across the full width of the main window.
- The singer rotation, Karaoke Deck and optional preview therefore gain the space previously used by both music decks.
- Music-only crossfade / Play Music controls and the MUSIC search selector are hidden.
- If music is playing when Karaoke Only Mode is enabled, hidden music playback is stopped safely; the playlists themselves are not deleted.
- Karaoke START/STOP/END does not attempt to fade or resume background music while Karaoke Only Mode is active.
- Switch the menu option off to restore Deck 1 and Deck 2 with their queued playlists intact.
- The selected host mode is saved in `%LOCALAPPDATA%\Hazz Karaoke Hoster\ui-layout.json` and restored on the next launch.
- The header clearly shows **KARAOKE ONLY MODE** or **KARAOKE + MUSIC MODE**.

### v0.14.2 fixes retained
- Non-blocking Kamikaze random selection for multi-million-song libraries.
- Restored CD+G visual-render/timer/completion helpers from the v0.14.2 compile hotfix.
- GUI logo, responsive layout persistence, crash logging, database backup, VirtualDJ/other-hoster imports and all prior features remain included.

### Refined host GUI
- The supplied **Hazz Karaoke** logo is now built into the main GUI header as an application resource.
- The logo uses **FIT** scaling so none of the artwork is cropped or stretched.
- The header is more compact to preserve hosting space.
- Main window width/height/maximised state are now remembered, in addition to the Deck 1 / Karaoke / Deck 2 splitters, player/playlist heights and Preview visibility.
- Existing high-contrast menu/drop-down styles, ClearType text, layout rounding, minimum panel sizes and wrapping transport controls are retained.

### Stability / database safeguards
- Hazz now writes diagnostics to `%LOCALAPPDATA%\Hazz Karaoke Hoster\Logs` for UI/background/fatal errors.
- UI exceptions that can be contained are logged so a non-critical interface error is less likely to kill a live show. Memory-failure exceptions are not swallowed.
- **LIBRARY → Database → Open Database Folder** opens the real Hazz data location.
- **LIBRARY → Database → Backup Database…** creates a consistent live SQLite backup using SQLite's backup API, including data still represented through WAL.

### Import VirtualDJ and other hoster libraries
`IMPORT → Import Other Software` now contains:
- **VirtualDJ database.xml…**
- **Other hoster database / playlist…**

Supported read-only source types in v0.15:
- VirtualDJ / generic XML
- M3U / M3U8
- PLS / LST
- CSV / TSV / TXT exports
- SQLite `.db/.sqlite/.sqlite3`
- Access/KDB `.mdb/.accdb/.kdb`

Hazz previews detected records before importing. Choose **AUTO**, force **KARAOKE**, or force **MUSIC**. Optional path verification is available, but fast import can trust a pre-existing hoster's file locations to avoid a full disk crawl.

Imported source provenance is stored separately in Hazz. The original hoster database/list is never modified. Re-importing a playlist refreshes its Hazz entries rather than duplicating them.

Where practical, imported paths are used to infer Hazz watched library roots. New karaoke/music files added to those HDD folders can therefore be picked up by auto-watch or **LIBRARY → Rescan Watched Folders…** without re-importing the old VirtualDJ/Karma/etc. catalogue.

### Existing major functions retained
- Dedicated Karaoke deck plus two automatic-crossfade music decks.
- Adjustable Auto Crossfade, visual crossfader and FADE NOW.
- Played music leaves the live playlist while permanent dated Music History is retained.
- Music shuffle, drag-to-reorder and clean # / Artist / Title / Time playlists.
- Karaoke singer rotation, permanent singer names/history, drag singer reordering and per-singer song queues.
- Next 4 singers plus full numbered audience rotation scroller.
- Audience background FIT mode, persistent topmost logo and configurable fonts/colours.
- Kamikaze Karaoke, Find Alternative, Skip Silence, live key adjustment and ±0.25 s CD+G sync.
- CD+G/MP3+G/ZIP and installed-codec video/media playback paths.
- Multiple watched library folders and paged library browsing for very large collections.
- Read-only Karma complete-library migration and BPM Studio playlist/history migration.

## Build and run
1. Open `HazzKaraokeHoster.sln` in **Visual Studio 2026**.
2. Select **Release** and **x64**.
3. Choose **Build → Build Solution**.
4. Run `RELEASE\HazzKaraokeHoster\Hazz Karaoke Hoster.exe`.

`BUILD-EXE.cmd` performs the same self-contained Windows x64 publish.

NuGet restore requires:
- Microsoft.Data.Sqlite 10.0.11
- System.Data.OleDb 10.0.11
- NAudio 2.2.1

## v0.15 suggested test
1. Build Release/x64 and start Hazz. Confirm the supplied logo is fully visible in the top-left header.
2. Resize/maximise Hazz and adjust several splitters; restart and confirm the layout is restored.
3. LIBRARY → Database → Backup Database and confirm a `.db` backup is created.
4. IMPORT → Import Other Software → VirtualDJ database.xml and inspect the preview before importing.
5. For a small test source, import and confirm tracks appear in the correct Karaoke/Music search mode.
6. Add a new file to an inferred/watched library folder and confirm auto-watch or Rescan Watched Folders adds it.
7. Continue normal karaoke/music playback testing to verify the UI/import changes have not changed the existing deck workflow.

## Test-build note
The source has been statically validated in the generation environment, but the Windows .NET 10/WPF SDK is not installed here. Visual Studio 2026 on your PC remains the authoritative compiler/runtime test.
