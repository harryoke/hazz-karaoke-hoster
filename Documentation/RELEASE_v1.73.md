# Hazz Karaoke Hoster v1.73 — Side-list virtual folders and music-video display controls

Made For KJ/DJ's By A KJ/DJ

## Music virtual folders inside the Side List

When **Single Deck + Side List Mode** is active, press **VIRTUAL FOLDERS** in the Music Side List controls. The right-hand music pane changes from the holding list to a folder browser without opening a separate Library Browser window.

The tree uses Hazz's existing virtual-folder database, so it includes normal Hazz folders plus folder trees imported from supported software. In particular, BPM Studio `.GRP` / `.PLG` archive groups imported beneath the BPM Studio folder appear here automatically. No duplicate import or second folder database is created.

- **ALL MUSIC** shows the complete indexed music library.
- Selecting a virtual folder shows only Music tracks linked to that folder.
- Results are paged in batches of 250 so large libraries do not have to be loaded into memory at once.
- Multi-select tracks and press **ADD TO SIDE LIST** to copy them into the holding list.
- Press **SEND TO DECK 1 →** to queue the selected tracks directly on Deck 1.
- Double-click a track to add it to the Side List.
- Drag one or several selected tracks from the folder result list onto Deck 1 or the Side List using the existing playlist drag/drop system.
- **REFRESH** reloads the folder tree. A completed BPM Studio import also refreshes an open Side List folder browser automatically.
- **BACK TO SIDE LIST** returns to the normal holding list. Hazz remembers whether the folder browser was shown.

Media files are never moved or copied by this browser. It reads the existing library and virtual-folder links.

## Music-video Fit / Stretch

Open **DISPLAY → Audience Settings — Backgrounds, Logo, Scroller…** and find **MUSIC VIDEO DISPLAY**.

**Video fit** has two choices:

- **Fit** — preserves the video's aspect ratio and shows the complete frame. Letterboxing/pillarboxing may appear.
- **Stretch** — fills the audience video area by stretching the frame to the output dimensions.

This setting applies only to music videos. It does not change the existing karaoke/CD+G Fit/Stretch control.

The setting works with both the Windows video engine and the LibVLC audience engine.

## Music-video transitions

The same **MUSIC VIDEO DISPLAY** section provides a transition and duration. These are visual video transitions only; existing music-deck audio fades/crossfades remain independent.

Available transitions:

- **Cut** — video appears immediately.
- **Fade from Black** — a black cover fades away to reveal the new video.
- **Wipe Left** — the black cover moves left to reveal the new video.
- **Wipe Right** — the black cover moves right to reveal the new video.

Duration is adjustable from **0.10 to 5.00 seconds**. For Fade/Wipe, Hazz waits for the audience video decoder's first ready frame before starting the reveal. This prevents the transition from completing while the decoder is still opening the file.

The transition overlay is hosted with the audience overlays, so it remains visible above either Windows video or LibVLC's native video surface.

## Retained behavior

v1.73 is based directly on the supplied v1.72 source and retains its Sound FX bank, v1.71 audience-preview fix, v1.7 AV sync/transparent-CD+G features and earlier BuildFix behavior.
