# Hazz Karaoke Hoster v1.74.2 — BPM Studio MP3 tags

v1.74.2 fixes BPM Studio imports so embedded MP3 Artist/Title tags are used instead of relying only on the filename.

## Fixed

- BPM playlists and daily history now read MP3 Artist/Title tags.
- BPM GRP/PLG virtual-folder imports use the same tagged metadata.
- Tags are read **once per unique MP3**, even when the same track appears in many BPM playlists/history lists.
- Tagged values are applied consistently to:
  - the main Music library
  - imported BPM playlists
  - BPM history
- Re-importing the same BPM Studio data repairs existing tracks that older Hazz BPM imports indexed from filenames.
- Missing, locked, malformed or untagged MP3s safely keep filename-derived fallback metadata.

## Re-index existing BPM music

If tracks already imported from BPM Studio show the wrong Artist/Title, import the same BPM source again. v1.74.2 reads the MP3 tags and updates the existing Hazz records where real tags are present.

All v1.74.1 Search Columns functionality and all v1.74 Music Video fixes/features remain included.
