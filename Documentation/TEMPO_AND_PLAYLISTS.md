# Tempo, singer entry and clearing a playlist

## Change the speed of the playing song

1. Start a karaoke song, a music track or a music video.
2. Click **TEMPO**, beside **ADD SINGER**. This shared control follows the playing song. Karaoke takes priority while a singer is performing. Otherwise it controls quick-play music or the active music deck. During a crossfade it controls the outgoing deck until the handover finishes.
3. Check the song name at the top of the tempo window.
4. Move the slider. **100%** is normal speed, **75%** is slower and **125%** is faster. Arrow keys adjust in 1% steps. The musical key stays unchanged.
5. Close the window to keep that tempo for the current playback only. The window also closes when playback changes tracks, so you cannot accidentally edit the next song.

Tempo returns to **100%** when the song finishes or is stopped. A different song starts at 100% unless it has its own saved preference. Pausing and resuming keeps the current tempo. Music crossfade timing follows the adjusted speed.

The position slider and time counters refer to positions in the original recording, so lyrics and video can follow the same timeline. They are not a wall-clock countdown at altered speed.

## Remember a tempo

1. With the desired song playing, click **TEMPO** and set its speed.
2. Click **SAVE FOR THIS TRACK**. Hazz recalls that speed whenever the same file is loaded, including after restarting the app.
3. During a queued singer's performance, you may instead choose **SAVE FOR THIS SINGER AND SONG**. This takes priority over the track default for that singer at that venue. Without an active venue, it belongs to the default singer list.
4. To save normal speed again, click **RESET TO 100%**, then the appropriate Save button. Reset alone changes only the current playback.

The original audio/video/ZIP is never modified and no new recording is exported. Preferences are stored in `tempo-preferences.json` beside Hazz's other local settings and are included in the normal database/settings backup. Preferences follow the file path; moving or renaming a file means it needs a new saved preference. Singer preferences use venue and singer name, so a renamed singer needs their preference saved again.

Karaoke video and CD+G graphics follow the adjusted playback timeline. Both Windows and VLC audience video engines support the setting. If Hazz cannot decode a karaoke file's audio for processing, tempo is unavailable for that file rather than changing its pitch unexpectedly.

## Add a singer with Enter

1. Type the full name into **SINGER NAME**, or choose a saved name.
2. Press **Enter**, or click **ADD SINGER**.
3. The singer appears in the rotation and the name field clears. An already queued singer is selected instead of creating a duplicate.

The optional song-title box is removed. Assign songs by dragging a karaoke search result onto the singer, or open the singer's song list.

## Empty a deck playlist

1. Under the relevant deck's playlist, click **CLEAR PLAYLIST**.
2. Confirm that you want to empty that list.
3. All rows are removed from that deck. The song already playing continues; use its Stop button if you also want to stop it. The other deck is not cleared and may still play through normal automation.

The side list also has a clear command. This empties its holding list. Clearing does not delete media files, library entries, saved named playlists or music history. To retain a particular queue order for later, choose **SAVE PLAYLIST** before clearing it.
