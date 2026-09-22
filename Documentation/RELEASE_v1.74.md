# Hazz Karaoke Hoster v1.74 — Music Video library, search restore and expanded transitions

v1.74 builds directly on v1.73 BuildFix2. The v1.73 LibVLC project fix and the Single Deck + Side List consecutive-video fix are retained.

## Dedicated Music Video imports and library

Use **IMPORT > Import Music Video Files…** to select one or many video files, or **IMPORT > Import Music Video Folders…** to scan complete folders/subfolders.

Music videos are now stored as the separate `MusicVideo` media kind. They have their own:

- library count (`Videos`)
- main search mode (`MUSIC VIDEO`)
- full Library Browser mode (`MUSIC VIDEOS`)
- Single Deck + Side List virtual-folder view (`MUSIC VIDEOS`)

The video files stay where they already are; Hazz only indexes them.

On first database initialisation under v1.74, video files that older Hazz versions stored as ordinary `Music` are reclassified as `MusicVideo`. No re-import is required and files are not moved.

For backwards compatibility, existing watched Music folders can still contain a mixture of audio and video. Audio is indexed as `Music`; video is indexed as `MusicVideo`. Dedicated Music Video roots/watchers use the new video-only import mode.

BPM Studio imports also classify referenced video formats as `MusicVideo`, so BPM virtual folders can be viewed separately as MUSIC or MUSIC VIDEOS in the Side List browser.

## Previous search results reopen on click

If a search has already been performed:

1. Close/hide the search-results overlay.
2. Do not change the search text.
3. Click back into the search box.

The previous result set reopens immediately. A dummy keystroke is no longer required.

## Expanded music-video transitions

Audience Settings > **MUSIC VIDEO DISPLAY > Transition** now includes:

- Cut
- Random Fancy
- Fade from Black
- Fade from White
- Flash White
- Wipe Left / Right / Up / Down
- Zoom Reveal
- Spin Zoom
- Curtain Horizontal / Vertical
- Diagonal Sweep
- Neon Sweep

**Random Fancy** chooses a different non-cut effect whenever a new music video is loaded.

The existing duration slider still controls the visual transition. Transitions begin when the video surface reports that the new video is ready, reducing black/loading-frame reveals. These transitions affect picture presentation only; Deck audio timing, volume fades and crossfades remain separate.

## Existing fixes retained

- v1.73 BuildFix1: normal LibVLC NuGet/MSBuild project evaluation; no dependency on the missing `CollectVlcFilesToCopyWindows` target.
- v1.73 BuildFix2: in Single Deck + Side List mode the muted audience-video mirror does not own playlist end/advance events, preventing video 2 from being cleared while its audio continues.
- Music Video Fit / Stretch remains available.
- Two-deck video playback/transition behaviour remains unchanged.
