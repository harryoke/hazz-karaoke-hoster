Current release: v1.5. See [v1.5 instructions](RELEASE_v1.5.md) for drag-edge scrolling, folder-only Kamikaze, skin choices, saved-data protection and new playback controls.

# Music tags and playlist menu — v1.3

Extract the whole Windows ZIP into a new folder and run Hazz Karaoke Hoster.exe.
Keep the accompanying files. These controls are included in v1.3.

## MP3 artist and title

Add MP3 files to either music deck normally. Hazz reads their embedded artist and
title in the background and updates the playlist and scrolling deck display.
Blank or unreadable tag fields retain the existing library information or filename.
Reading tags does not change your files. Music-folder scans also read tags; rescan
your music folder if you want existing library search results updated. BPM imports
remain fast: they use their existing import process, with tags read when songs enter
a deck. Karaoke filename parsing is unchanged.

## Right-click a track

Right-click the row you want. For several tracks, select them first with Ctrl-click
or Shift-click, then right-click one of the selected rows.

- **Play next on this deck:** puts selected queued songs directly after the current
  song, keeping their playlist order. It does not interrupt playback or change your
  choice of which deck plays next. In side-list mode this action is unavailable for
  the side list; move its tracks to Deck 1 first.
- **Move to Deck 1 / Deck 2 / Side List:** moves selected queued songs to the end of
  the destination list. The current song stays on its player.
- **Add to virtual folder:** choose an existing virtual folder and press Add. Files
  stay in their original locations. Create folders using Library first. Missing
  tracks that cannot be indexed are skipped, and the status shows the added count.
- **Edit MP3 tags:** select one MP3 and edit Artist, Song title and Album. Press
  Save changes to write to the file. Close the window to cancel. Other tags are
  retained. Files loaded in a player, including paused or cued tracks, cannot be
  edited until you load another file. The save uses a temporary copy before replacing
  the original, so allow enough free space for a copy of that MP3.
- **Mark as favourite / Unmark favourite:** adds or removes a yellow star. You can
  do this for several selected tracks. Favourites follow the file path across both
  decks and are saved between sessions; they do not modify MP3 tags. The star has
  a dark background so it remains visible on the yellow playing row.
- **Show in folder:** opens Windows Explorer with the selected file highlighted.
- **Locate missing file:** choose the file's new location. Matching entries in both
  current playlists are replaced, and the selected file is indexed. This is a
  playlist repair; it does not rewrite old saved playlists or historical file paths.
- **Track information:** shows the path, artist, title, album, duration, audio bitrate,
  sample rate, favourite status and saved global tempo where available.

## Other fixes included

Restart Song starts loaded karaoke again from zero. Play during active karaoke
leaves it running. Background controls fit at large GUI text sizes, including GIF
speed. Deleted or emptied BPM virtual folders can be imported again from unchanged
source files without resetting the database.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
