# Hazz Karaoke Hoster v1.2

## Playback and playlist additions

* Shared TEMPO controls the playing karaoke song, music or music video, from 75–125%, keeping pitch unchanged. Unsaved changes reset after the song; saved track settings return when that track is played again. Optional singer/song settings take priority at that venue.
* Enter in the singer-name field now adds/selects the singer. The unused optional song-title field is replaced by TEMPO.
* CLEAR PLAYLIST empties either deck while its playing song continues. Media files, library records and saved named playlists are kept.
* [Step-by-step instructions](https://harryoke.github.io/hazz-karaoke-hoster/tempo.html).

## Audience display improvements

* LOAD NEXT SINGER prepares the host preview without covering the venue display
  with a black frame. Karaoke takes over when PLAY is pressed.
* Singer names wrap in full, with song details underneath. Singer fonts go up
  to 192; the panel fits the available screen and shrinks when necessary.
* NEXT SINGERS has its own Heading size selector, default 36, up to 128.
* The scroller's Move away from edge slider shifts text inward from the top or
  bottom to compensate for a TV cropping the screen. Range: 0–250.
* DISPLAY > Audience Text Outlines provides separate enable, colour and 0.5–8 px
  width controls for headings, position numbers, names, songs, rotation text,
  venue messages and Kamikaze text. These apply live and save on closing.
* Outlined headings retain the final word when Windows rounds their width.

Open DISPLAY > Audience Settings for sizes and scroller placement. Settings
persist between sessions and are included in venue profiles. Outline widths
scale with Windows DPI and the singer panel's automatic fit.

v1.1 features remain included: main-console text sizing, Windows/VLC audience
video selection with Windows fallback, corrected CD+G colours and crisp/smooth
CD+G scaling. Untick DISPLAY > Smooth CD+G picture for crisp pixels.

## Download and run

Extract the complete Windows x64 ZIP into a new folder. Close your running Hazz
copy and open Hazz Karaoke Hoster.exe. Keep all DLLs and the libvlc folder beside
the EXE. This uses your normal Hazz database/settings; no separate VLC install
is needed. Source is provided for Visual Studio 2026/.NET 10.

Build and audience regression checks passed, including four long names at the
largest size, top/bottom scroller insets, outline pixels, independent stroke
settings and rounded-width heading rendering. Test your own laptop and audience
display before a show; these checks are not an hours-long playback soak test.

Tempo tests verified mono/stereo audio duration and pitch at 75%, 100% and 125%,
saved preference precedence and reset, Enter-to-add and both playlist clear actions.
Windows and VLC video clocks were checked at 75% and 125%, with pause and seek.

The included User-Guide.md covers the new options. Existing v1.0 PDF manuals
are historical and do not include these additions.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
