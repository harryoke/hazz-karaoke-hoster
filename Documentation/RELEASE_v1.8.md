# Hazz Karaoke Hoster v1.8

Made For KJ/DJ's By A KJ/DJ

## Audience display improvements
- CALL NEXT SINGER on the main console loads the next eligible singer's track and displays their call-up card. Press PLAY when ready.
- Optional automatic next-singer call-up after a song, with a 3–15 second duration.
- NOW SINGING card when playback starts, with a 3–15 second duration.
- Coming Up list configurable from 1–4 singers.
- Optional live queue status with active singers, HOLD count and approximate rotation time.
- Optional venue/event header.
- Temporary announcements with SHOW/CLEAR and 5–30 second duration.
- Editable wording, font, text size (12–160 pixels) and colour for the new overlay text elements.
- Overlay animations: Cut, Fade, Slide, Zoom, Pop and Random.
- Slideshow interval 20–60 seconds; Cut, Fade, Slide Left, Slide Right, Zoom and Random transitions.

## Fixes
- Corrected garbled queue-status and song/artist separators.
- Now Singing follows actual Play rather than file loading; stopping clears it immediately.
- Preview-only audience timers expire correctly. Removed the competing delayed song-completion handler.
- Information overlays respect karaoke, Kamikaze and music-video visibility settings. Queue and venue headers no longer share the same row.
- Slideshow transitions release opacity correctly for transparent CDG backgrounds.
- Audience enhancement controls wrap on narrow screens and labels follow the chosen skin.
- Broad searches run off the UI thread; existing cancellation and result limits remain.
- Reduced settings-file writes while adjusting sliders.
- Fixed Visual Studio packaging omitting VLC libraries/plugins. Both portable and optional single-EXE versions include the runtime.

All v1.74.8 features and subsequent audience test improvements are retained. Existing databases, singer histories, favourites and settings remain in place. Audience enhancement settings retain their existing filename for compatibility.

## Downloads and instructions
Choose Windows x64 for the portable folder, Single EXE for one application file, or VS2026 Source to build it yourself. Close your old Hazz before opening the new version. Extract into a new folder; do not mix files from different versions.

Read [Audience display v1.8 instructions](https://harryoke.github.io/hazz-karaoke-hoster/v1.8.html). The older v1.7 PDF is a base manual; the v1.8 guide covers the new controls.

## Validation
Release solution built with zero warnings/errors. Focused search, BPM re-import, MP3, CDG sync/transparency, audience and layout checks passed. Windows/VLC playback and muted preview checks passed, as did 60 live preview skin/size changes. Both release formats were checked for bundled VLC and SQLite. ZIP integrity is checked before upload.
Physical TV/audio-device and multi-hour show rehearsal remains recommended; automated checks do not certify every hardware combination.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
