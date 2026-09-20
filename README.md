# Hazz Karaoke Hoster

Free-to-use Windows software for hosting karaoke nights, playing background music and showing music videos on a separate audience screen.

Hazz brings a karaoke player, singer rotation and two music decks into one console, with tools for organising large libraries and preparing the next track during a live show.

![Hazz Karaoke Hoster v1.6 console with two music decks, karaoke controls, singer rotation and sound-device settings](Documentation/Images/console_v1.6.png)

*v1.6 Classic console shown with illustrative sample rows.*

**[Download for Windows](https://github.com/harryoke/hazz-karaoke-hoster/releases/latest)** · **[Official website](https://harryoke.github.io/hazz-karaoke-hoster/)** · **[User guide](Documentation/V1_MANUAL.md)**

## New in v1.6

Fit/Stretch karaoke playback; independent singer-rotation and custom-message switches; a second scrolling message with its own font, size, speed, colour and edge inset. The two bars stay on opposite edges. [Read the v1.6 guide](Documentation/RELEASE_v1.6.md).

## Retained from v1.5

Folder-only Kamikaze, scrolling while dragging through long lists, four complete skins, Karaoke Focus, karaoke seeking, track lengths, history CSV export and saved-data protection. [Read the v1.5 instructions](Documentation/RELEASE_v1.5.md).

## Retained from v1.4

Choose how background music gives way to karaoke: fade and stop (default), fade and pause, or fade and continue silently. See real audio waveforms on both decks and click to seek. Save singer photos per venue, show them in the singer list and on TV, and adjust their size and fit. Search favourites now show yellow stars.

[Step-by-step v1.4 instructions](Documentation/RELEASE_v1.4.md)

## What you can do

- **Host karaoke:** play MP3+G/CD+G, ZIP karaoke and video tracks, with key changes, lyric synchronisation and saved tempo preferences.
- **Manage singers:** add singers, keep their song history and organise the rotation. Manual ordering is the default; optional computer-assisted rotation methods let you choose the rules.
- **Run the music:** use two music decks with playlists, scrolling track displays, seeking and crossfading, or turn Deck 2 into a side list.
- **Organise your library:** search karaoke, music and music videos separately. Create named virtual folders and subfolders without moving media files.
- **Bring existing collections across:** import supported libraries, playlists and singer-history formats from other software, including BPM Studio and MediaMonkey workflows.
- **Control the audience screen:** show karaoke or music videos on a separate display, with configurable singer names, logos, messages, text outlines and scrolling text.
- **Personalise backgrounds:** choose images, animated GIFs or muted looping videos, or run a folder slideshow. Adjust image fit, GIF speed and scroller placement.
- **Set up your sound:** choose output devices for individual players and use optional audio levelling. Choose Windows or VLC for audience video playback.
- **Prepare for a show:** use venue profiles, next-track readiness checks, database backups and recovery tools. Adjust the console text size for readability.

## Get started

1. Download the **Windows portable ZIP** from the [latest release](https://github.com/harryoke/hazz-karaoke-hoster/releases/latest).
2. Extract the entire ZIP to a folder. Keep the DLLs and `libvlc` folder alongside **Hazz Karaoke Hoster.exe**.
3. Open the app and use **IMPORT** to add your music and karaoke folders.
4. Choose your outputs under **SOUND DEVICES**, and configure the audience screen under **DISPLAY**.
5. Add a singer and a song, or add music to a deck. Check the sound and audience output before starting your show.

You supply your own media files. Hazz does not include a song collection.

## Help and documentation

- [Complete user guide](Documentation/V1_MANUAL.md)
- [Tempo and playlist controls](Documentation/TEMPO_AND_PLAYLISTS.md)
- [Virtual folders guide](Documentation/VIRTUAL_FOLDERS_GUIDE.md)
- [v1.2 instructions](Documentation/RELEASE_v1.2.md)
- [Release history](CHANGELOG.md)
- [Report a problem or request a feature](https://github.com/harryoke/hazz-karaoke-hoster/issues)

## Music playlist tools

Read MP3 tags, edit artist/title/album, mark favourites with yellow stars, queue songs next, move tracks between decks, and add them to virtual folders from the right-click menu.

See the [playlist instructions](Documentation/MUSIC_PLAYLIST_MENU.md) and [v1.3 release notes](Documentation/RELEASE_v1.3.md).

## Building from source

Use Visual Studio 2026 with the .NET desktop development workload and .NET 10 SDK.
Open the solution, restore its packages and build the Windows app. For a ready-to-run
copy, use the packaged Windows download above rather than GitHub's automatic source ZIP.

## Copyright and licence

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
Unchanged copies may be shared free with all branding and notices intact. Redistribution
of renamed, rebranded or modified versions requires written permission.

See the [full licence terms](LICENSE.txt). This is source-available software. Third-party
components retain their own licences; see [ThirdParty](ThirdParty/README.md).
