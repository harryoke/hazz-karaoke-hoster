# Hazz Karaoke Hoster v2.10

Made For KJ/DJ's By A KJ/DJ

This release adds music-deck effects and fixes exact singer-list column sizing across restarts. All v2.01 features remain included: Singer Database Manager, crossfade presets, quick/deep Library Health scans and all skins.

## Flanger, reverb and echo

1. Start a music track on Deck 1 or Deck 2.
2. Click MUSIC FX beside CURVE in the music crossfade controls.
3. Choose Flanger, Reverb or Echo for the deck and tick EFFECT ON.
4. Adjust Effect level to blend the effect with the original audio. Start around 15–25%. Zero percent is dry; the maximum is 60%.
5. Use BYPASS THIS DECK or BYPASS BOTH DECKS to fade the effects out.

MUSIC FX turns green and reads MUSIC FX ON while either deck is enabled. The window stays separate so you can continue using the console. Closing the effects window does not bypass an enabled effect.

Flanger adds a moving short-delay sweep. Reverb adds a room-like tail. Echo repeats at a fixed 320 milliseconds. One effect per deck is available; these are built-in effects, not VirtualDJ plugins or a beat-synchronised effects chain.

Music-video audio on the music decks also receives effects. Karaoke audio, search preview and soundbite pads are unaffected. The existing output-device routing, deck volume, crossfades and mute still apply to the processed audio.

Effects switch off on Stop, Close or natural track end. Enable them again for the next song. Seek and resume clear previous delay audio. The preloaded-player handoff keeps the controls associated with the correct deck. Selections and levels are session-only, not saved to songs or venue profiles. Deck 2 effects are unused when it is a sidelist.

## Exact singer-list column widths

1. Drag the boundaries between headings in the main karaoke singer list to your preferred widths.
2. Close Hazz normally.
3. Reopen Hazz. The displayed widths are restored, including narrowed columns.

The earlier test saved some automatic/proportional sizing rules, allowing columns to shift when startup content was measured. v2.10 saves the actual displayed widths instead. Set your preferred sizes once in this version if upgrading from the earlier test. Sizes remain exact at the same Windows display scaling; on a smaller window, use horizontal scrolling instead of expecting the columns to shrink automatically.

Widths are also included when using SAVE CURRENT in Venue Profiles. Applying a saved venue setup restores its saved widths. Older profiles without column settings leave the existing widths unchanged. This is one saved column layout, not separate widths for each skin or console mode. A forced termination before normal closing may lose unsaved adjustments.

## Installation and previous features

Close the older Hazz instance. Extract the complete Windows ZIP into a new folder and run Hazz Karaoke Hoster.exe. The interface should show v2.10. An optional single-EXE ZIP and complete Visual Studio 2026 source are also available. Existing user databases and settings remain in their normal location.

For Singer Database Manager, crossfade presets and Library Health instructions, see RELEASE_v2.01.md or the website’s v2.01 guide. Existing PDFs are older base manuals; this guide supplies the new instructions.

## Validation

Automated tests cover mono/stereo effect processing at 44.1/48 kHz, dry bypass, stereo separation, reset/tail clearing, bounded output and effect changes. Column tests compare measured widths after serialisation/reload with different startup text and a narrower viewport. Layout checks cover all skins and console modes at normal/large text and multiple resolutions. Release build and bundled VLC/SQLite checks are also run.

Physical mixer listening, hardware latency and a complete live-show rehearsal remain unverified by these automated checks.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
