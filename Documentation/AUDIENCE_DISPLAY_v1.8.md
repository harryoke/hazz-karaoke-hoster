# Hazz Karaoke Hoster v1.8 audience guide

Made For KJ/DJ's By A KJ/DJ

## Calling the next singer

1. Finish or Fade Stop the current karaoke performance.
2. Press **CALL NEXT SINGER** on the main karaoke deck.
3. Hazz loads the first non-HOLD singer with a queued song and shows their call-up card.
4. Press **PLAY** when they are ready. Calling a singer does not start the song or record a performance.

The existing **LOAD NEXT SINGER** button remains available for loading without a call-up.
Manual call-up works even if automatic call-up is switched off. The automatic option only
shows a card after a song completes; it does not automatically load or start another song.
The audience display must already be sent to the TV to see the card there. Otherwise it
is prepared for the audience preview. Calling does not open a new TV window on the laptop.

## Changing the new audience text

1. Open **DISPLAY**, then Audience Display Settings.
2. Scroll to the slideshow and audience enhancement controls.
3. Press **EDIT OVERLAY TEXT / FONTS / COLOURS**.
4. Choose a text element: call-up heading/name/song/message, Now Singing heading/name/song,
   announcement, queue status or venue header.
5. Edit its wording, font, size (12–160 pixels) and colour. The colour chooser is optional;
   a hexadecimal colour can also be typed.
6. Press **SAVE**, or **CANCEL** to discard the edits.

Keep `{singer}`, `{song}`, `{message}`, `{queue}` and `{venue}` where you want live information.
For example, `Please welcome {singer}` follows the current call-up singer.
Use the corresponding token in its own element: `{singer}` in a singer element, `{song}` in
a song element, and so on. Remove it if you want fixed wording instead. Blank text hides
the wording. The call-up card scales down when necessary to fit a long name on the display.

The settings remain in `audience-enhancements-test.json` alongside the existing Hazz settings.
These options are global, not separate venue-profile settings. Existing font/background,
singer history, favourites, library and normal audience settings are not replaced.

The music-video singer overlay switch also controls call-up cards while a music video is showing.

## Fixes checked in this review

- Fixed the Visual Studio release publish step omitting VLC libraries/plugins from both output formats.
  Publishing now runs the package build stage so the native runtime is included.
- Now Singing starts on Play, not merely when a decoder opens a loaded file.
- Finishing/stopping karaoke clears Now Singing immediately. Call-up no longer uses a competing
  delayed MediaEnded handler or skips the first eligible singer because of a stale active-singer reference.
- Call-up and announcement timers are connected even when the audience window is only used by preview.
- Queue/venue/call-up information respects the existing music-video singer-overlay switch and Kamikaze screen.
- Queue status and venue header are stacked so they do not occupy the same line.
- Enhancement settings rows wrap; label colours follow skin changes.
- Slideshow fades release their opacity animation so later CDG background opacity changes work.
  The outgoing slide is cleared when switching to karaoke or music video.
- Broad SQLite searches run on a worker thread with the existing cancellation and result caps retained.
- Slider edits update the display immediately; settings-file writes are debounced and pending changes
  are saved on close.

## Slideshow, cards and announcements

1. Open DISPLAY > Audience Display Settings and choose your background folder.
2. Set Next slide to 20–60 seconds and select a slideshow transition. These are picture effects; background videos remain muted.
3. Choose Coming up: 1–4 to set the number of upcoming singers.
4. Enable Show NOW SINGING and choose 3–15 seconds to introduce performers when Play starts.
5. Enable Automatic NEXT SINGER call-up and choose its duration if wanted. CALL NEXT SINGER remains available with this automatic option off.
6. Enable Show queue status / approx time for a live summary. It estimates unknown track lengths at four minutes and is not a guaranteed finishing time.
7. Enable Venue header and enter the event or venue name.
8. Choose Overlay animation: Cut, Fade, Slide, Zoom, Pop or Random.
9. For a temporary announcement, enter the message, choose 5–30 seconds, then press SHOW. CLEAR removes it early.
10. Use EDIT OVERLAY TEXT / FONTS / COLOURS to personalise each new text element.

The queue and venue information hide during karaoke. The music-video singer visibility switch also controls these information cards while music videos play. Existing logo, scrollers, photos and CDG/video settings retain their own controls.

## Building and testing

Open `HazzKaraokeHoster.sln` in Visual Studio 2026 with the .NET 10 desktop workload.
Allow NuGet restore, select Release and Build Solution. The release project creates
`RELEASE/HazzKaraokeHoster/` and the optional `RELEASE/SingleFile/` executable.

Focused tests live under `tests/ReviewChecks`, `tests/VirtualFolderChecks`,
`tests/SearchPunctuationChecks`, `tests/CdgTransparencyChecks`, `tests/Mp3Checks`,
`tests/SmartImportChecks`, `tests/AudienceDisplayOptionsChecks`, `tests/SkinChecks` and
`tests/LibVlcSmoke`. Run audience checks with `--audience-only`.
The video smoke test needs a local video longer than 40 seconds because it seeks to 30 seconds.

Before using the new version for a show, rehearse call-up → Play → Pause → Fade Stop on your actual TV,
check the new text editor at your chosen GUI text size, and try your own slideshow transitions.
Multi-monitor hardware placement and an hours-long live show cannot be certified by the short tests.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
