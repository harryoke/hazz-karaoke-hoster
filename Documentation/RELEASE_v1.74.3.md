# Hazz Karaoke Hoster v1.74.3 — Music lengths and Search Columns fix

v1.74.3 fixes two issues found during live use.

## Music track lengths

- Music deck and Side List rows now show cached track length before playback starts.
- If a duration is not already cached, Hazz reads it in the background and stores it for next time.
- This also covers tracks added from saved playlists, Music Archive, virtual folders and direct file paths.
- Music search now reads/caches track lengths whenever the **Length** column is enabled.

## Search Columns

- Fixed a mode-switching bug where Karaoke **Length** could be saved as hidden after switching to Music search.
- Karaoke, Music and Music Video keep independent column visibility, width and order settings.
- Existing column settings are preserved; if Length was previously lost by the bug, enable it once again and it will now remain saved correctly.

All v1.74.2 BPM MP3-tag fixes and earlier v1.74.x Music Video/Search Columns features remain included.
