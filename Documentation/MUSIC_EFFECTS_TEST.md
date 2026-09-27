# Music effects test build (based on v2.01)

Made For KJ/DJ's By A KJ/DJ

This is an unpublished test build. All v2.01 features remain included.

## Try the effects

1. Close your previous Hazz instance. Extract the whole test ZIP to a new folder and open Hazz Karaoke Hoster.exe. The console says v2.01 FX TEST.
2. Start a music track on Deck 1 or Deck 2.
3. Click MUSIC FX beside the crossfade CURVE control.
4. Choose Flanger, Reverb or Echo for the deck, then tick EFFECT ON.
5. Move Effect level to blend the processed sound with the original. Start around 15–25%. Zero percent is unchanged sound; the maximum is 60%.
6. Press BYPASS THIS DECK or BYPASS BOTH DECKS to fade the effect out. The main MUSIC FX button turns green and says MUSIC FX ON when either deck is enabled. You can close the effects window and continue using the console; closing it does not bypass the effects.

Flanger creates a moving, short-delay sweep. Reverb adds a room-like tail. Echo adds repeating delays, set to 320 milliseconds in this first test. These are built-in effects, not VirtualDJ plugins. This first version has one effect per deck, not an effects chain or beat-synchronised controls.

Music-video audio on the decks also passes through these effects. Karaoke audio, search preview and the nine soundbite pads remain unaffected. Existing music output-device selection, deck volume, crossfades and mute controls still apply after the effects.

Effects switch off on Stop, Close or natural track end. Enable again for the next song. Seek and resume clear the old delay buffer so a previous part of a song does not echo after the jump. Effect selections and level are session-only; they are not saved to venue profiles or song files. Deck 2 effects are not used when Deck 2 is a sidelist.

## Checks and remaining testing

Automated tests cover mono/stereo at 44.1/48 kHz, exact dry bypass, buffer boundaries, audible-signal changes, finite/bounded enabled output, left/right separation, clearing delays on reset, effect switching and invalid values. A stereo reverb CPU check processed 30 seconds of samples in about 110 ms on this machine, with no per-block allocations. That is not a prediction for all laptops.

Use the test build outside a live show first. Listening quality on the physical mixer, hardware latency and a full-length show remain unverified. The public GitHub release is still v2.01; this test is not published.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
