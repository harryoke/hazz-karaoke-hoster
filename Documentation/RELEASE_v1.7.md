# Hazz Karaoke Hoster v1.7 display and sync guide

Made For KJ/DJ's By A KJ/DJ

Audience settings on smaller screens: open DISPLAY > Audience Settings. Controls keep your chosen GUI text size instead of shrinking to fit the whole panel. Use the mouse wheel or the vertical scrollbar to reach the lower settings. CLOSE stays visible at the bottom. At 1366×768 you do not need to reduce Windows scaling to use this panel.

Version 1.7 adds video AV Sync, a complete audience preview and optional transparent CD+G backgrounds. Existing playback, singer history, venue profiles, two scrollers and Fit/Stretch controls remain available. Transparency starts off.

## Correct karaoke video timing with AV Sync
1. Load and play a karaoke video. Listen to the sound and watch the audience picture or preview.
2. Find CDG / VIDEO AV SYNC on the karaoke deck.
3. Press +0.25s when the picture or displayed lyrics are late. A positive value moves the picture ahead of the audio. For example, at audio time 10 seconds, +1 second shows video time 11 seconds.
4. Press −0.25s when the picture is early. A negative value delays the picture. At audio time 10 seconds, −1 second shows video time 9 seconds.
5. Use small steps until it looks right. The available range is −10 to +10 seconds. Sound continues normally; the control adjusts the picture.
6. Press SAVE to keep that track default. Press RESET for zero; press SAVE afterwards if you want zero to replace the saved default.

The same controls still adjust CD+G graphics. Video AV Sync works with Windows and VLC audience video, including pause, resume, seeking and the preview. Negative offsets hold the opening picture until audio reaches the required point; positions cannot run before the start or beyond the end of a video. This corrects a consistent offset, not a badly edited recording whose timing varies throughout the song. It does not adjust ordinary music-deck videos.

The track default is used when loading the file directly and is also saved to its library record when available. Existing queued singer/song sync preferences remain independent and take precedence when loading that singer's request. Adjust those in the singer's song/history window or while that singer's song is loaded. File-based defaults are matched by the original path, including the original ZIP path for zipped karaoke; moving or renaming a file changes that match. Saving does not rewrite the media file.

## Preview the whole audience screen
1. Press SHOW PREVIEW above the singers list if the preview is hidden.
2. The panel now shows AUDIENCE PREVIEW. Between songs it includes your background, singer names and photos, logo, both enabled scrollers and any Kamikaze announcement.
3. Open DISPLAY > Audience Settings and change your messages or layout. The preview follows the actual audience scene, including the same scroller positions and slideshow item.
4. Start karaoke or a music video. The preview follows playback and the audience overlay visibility rules. During karaoke, singer lists and scrollers remain hidden as configured by Hazz's normal karaoke display behaviour.
5. Press HIDE PREVIEW to recover space and release its extra video decoder. Showing it again reconnects to the current scene.

The preview works without opening a TV window, using a 16:9 preparation scene until an audience window supplies its size. When the audience window is open, the preview fits its proportions. It never sends an audience window to another display by itself. Normal TV and display-selection buttons still control where the audience sees the show.

The preview's video decoder is muted, so it cannot add a second copy of the audio. Native video is mirrored using a separate muted decoder and can differ by a few frames; this is a monitoring preview, not a frame-accurate recording. Backgrounds, photos and text use the shared audience scene. A powerful video may need more resources with preview enabled, so hide the preview if your laptop is struggling.

## Put your own background behind CD+G lyrics
1. Open DISPLAY > Audience Settings.
2. Enable Singer-view background. Choose an image, GIF or video, or choose a folder for the existing one-minute slideshow.
3. Enable Transparent CD+G background.
4. Leave Remove colour on Auto first. Hazz removes the colour identified by the CD+G memory-preset background instructions.
5. Play the CD+G song and check AUDIENCE PREVIEW. The chosen background now continues behind the remaining graphics during karaoke.
6. If Auto removes the wrong area, try Palette 0 through Palette 15 in Remove colour. These are the song's sixteen colour slots, not fixed names such as black or white. Choose the slot that removes the background while keeping the words readable.
7. Adjust Background opacity to dim your artwork towards black. Adjust Lyrics opacity separately; 100% keeps the remaining CD+G graphics fully visible.
8. Use the existing background Screen fit and GIF speed controls as needed. Karaoke Fit/Stretch still controls the CD+G picture itself.

CD+G is a coloured pixel image, not a separate lyrics layer. Any artwork or lyric pixels using the selected colour can also disappear. Lyrics opacity affects all remaining CD+G graphics, including title cards and drawings. Check each song and keep the background calm enough for the words to stay readable. This feature does not remove backgrounds from MP4 or other ordinary karaoke videos.

## Keep a CD+G adjustment for one song
1. Load the CD+G song and adjust transparency, colour slot and both opacity sliders.
2. Press SAVE FOR THIS SONG. Hazz stores that combination against the original song path. It works for a CDG/audio pair or a karaoke ZIP, without modifying either file.
3. Load the song again to recall the saved combination. The controls show the recalled settings.
4. Press USE DEFAULTS to remove that song's override and return to the general CD+G display defaults.
5. Press RESTORE ORIGINAL for an immediate opaque, full-opacity CD+G picture. If the song already has a saved override, press SAVE FOR THIS SONG afterwards to keep the restored appearance for future loads.

When no song override is active, changing these controls changes the general default. When an override is active, changes are temporary until SAVE FOR THIS SONG is pressed. Close Hazz normally to keep general settings. Venue profiles save the display settings under the existing venue save rules; an older profile without these options uses transparency off. Read the status line after saving: if storage is unavailable, Hazz leaves the show running and asks you to retry.

## Downloads and testing
The normal Windows folder ZIP and optional Single EXE ZIP contain the same features. Close your old Hazz, extract the new download and launch it. Keep every runtime folder with the normal ZIP. Both versions use your existing Hazz data location. The single EXE extracts its bundled runtime automatically. No separate VLC installation is required.

For Visual Studio 2026, install the .NET desktop development workload and .NET 10, open HazzKaraokeHoster.sln and build Release / x64. The normal package is produced in RELEASE/HazzKaraokeHoster, and the optional executable in RELEASE/SingleFile.

Validation covers compilation, CD+G alpha/keying and seek reset, saved-setting preservation, preview-only composition, four-skin layouts, Windows/VLC AV Sync, pause, seek, preview muting and fallback. Please rehearse on your own laptop, audience display and sound devices before using the update at a live show; a multi-hour hardware show test is not part of these automated checks.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
