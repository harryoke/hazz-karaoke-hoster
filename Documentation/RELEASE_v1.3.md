# Hazz Karaoke Hoster v1.3

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.

## New music tools

- Read MP3 artist and title tags in playlists, scrolling deck displays and music-folder scans, with fallback for missing or unreadable tags.
- Edit MP3 artist, title and album from the playlist. Saving uses a temporary copy and retains the audio. Loaded, paused and cued files must be unloaded before editing.
- Mark favourites with yellow stars, saved between sessions and shared across both decks. Search results also show the yellow star and support right-click marking for one or multiple selected tracks.
- Right-click for Play next on this deck, Move to the other deck/side list, Add to virtual folder, Show in folder, Locate missing file and Track information.
- Locate missing file repairs current playlist entries; old saved playlists and historical paths are not rewritten.

## Fixes and improvements

- Dedicated Restart Song button starts loaded karaoke at zero. Play while karaoke is already playing leaves it uninterrupted; Play while paused resumes.
- Display background controls have room to wrap at large GUI text sizes, including GIF speed.
- Re-import restores deleted or emptied BPM virtual folders even when their source files have not changed. Existing unchanged folders still use the fast cache.
- README now presents the app with a screenshot, feature summary and setup links. Historical notes are in CHANGELOG.md.

## Download and use

Download the Windows x64 ZIP, close Hazz, extract the entire archive into a new folder and run Hazz Karaoke Hoster.exe. Keep its DLLs and libvlc folder. Existing database and settings remain in their usual location.

Read MUSIC_PLAYLIST_MENU.md for step-by-step instructions. Rescan music folders to update existing library search metadata from MP3 tags. External imports retain their fast metadata import paths; tags are read when tracks are added to music decks.

## Validation

Release compilation, MP3 tag read/write checks, persistent favourites, WPF playlist actions, yellow-star rendering, audience controls and BPM folder restoration checks passed. Archive contents are verified before upload. These automated checks do not replace a multi-hour live-show test with your devices.
