# Hazz Karaoke Hoster v1.72 — Sound FX guide

Made For KJ/DJ's By A KJ/DJ

## Set up your nine soundbite buttons
1. Find FX 1 to FX 9 in the space along the top of the console. They are available in all four skins. On narrower layouts the buttons wrap onto additional rows.
2. Click an empty pad, or right-click any pad, to open its editor.
3. Press CHOOSE AUDIO FILE and select your own soundbite. MP3, WAV, WMA, M4A, AAC and FLAC are offered; playback depends on Windows supporting the file. WAV or MP3 is a good starting point. Files stay in their original location; they are not copied into Hazz.
4. Enter the button name. Long names are shortened on the button; hover to see the full name and file path.
5. Press CHOOSE COLOUR, or enter a colour such as #00AA55. Text changes between black and white for contrast. A playing pad becomes brighter.
6. Set the soundbite volume. Start low and check your speakers before a show.
7. Leave Fade music down while this soundbite plays ticked to duck Deck 1, Deck 2 and Space-bar quick-play music. Untick it to play the effect over the music. Karaoke vocals/audio are not ducked by this control.
8. Choose the Sound FX output. This output selection applies to all nine pads and is independent of the deck outputs. The default is the Windows default output device.
9. Press SAVE. Closing the editor without saving cancels your edits.

Pad settings are saved in sound-fx.json beside Hazz's database, with a previous copy retained when updated. The bank is shared across venues. No sound clips are bundled.

## Play, replace and stop effects
Click a configured pad to play it. Only one soundbite plays at a time: pressing another pad replaces the current one; pressing the same pad starts it again. STOP FX ends it early.

With ducking enabled, music fades down over approximately a quarter of a second. When the effect ends, music returns smoothly to its CURRENT fader level. Fader changes, normal crossfades and stopped/paused deck states remain in force. Soundbites are prepared silently before ducking. A missing file leaves music unchanged; playback failures restore the duck level and report the problem in the status line. A ten-minute safety timeout ends an overlong effect and restores music.

To clear a pad, right-click it, empty the file field, untick its Kamikaze link if selected, and SAVE. To replace a clip, choose another file and SAVE.

## Add a Kamikaze announcement sound
1. Configure a pad with your announcement soundbite.
2. Right-click that pad and tick Use this soundbite for Kamikaze announcements, then SAVE.
3. Add a singer and use KAMIKAZE normally. After a random song is successfully assigned, the audience announcement is requested and the soundbite plays with music ducking.
4. The effect finishes and music returns smoothly. STOP FX can end it early.

Only one pad can be linked to Kamikaze; linking a different pad replaces the old link. Untick the option to disable it. The link starts off. A failed random selection does not play the effect. It is also suppressed while a karaoke performance is playing or paused so it cannot interrupt that singer. Open the audience display to show the message on the TV; the soundbite does not move or open a display window.

Kamikaze always ducks music for its linked soundbite, even if that pad's normal ducking checkbox is off. Its volume and sound device are the same as when the pad is clicked manually.

## Update and retained features
Close Hazz, extract the release into a new folder and run Hazz Karaoke Hoster.exe. Existing singer lists, settings, library and favourites are retained. The v1.71 preview stability fix and all v1.7 display/AV sync features remain included. The v1.7 main manual still covers those controls; use this guide for the new Sound FX bank.

Validation included missing files, failed audio output, actual playback completion using a silent WAV, rapid replacement, repeated Stop FX, fader changes during ducking, settings round trips, and all four skins at normal/large text and 1920/1366/1024 widths. Test your own clips and sound device before a live show.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
