# Hazz Karaoke Hoster Dummy Guide

Simple steps for a first live show.

## The ten minute setup

### The ten minute setup

- Connect your second screen or projector and set Windows to Extend these displays.
- Open Hazz Karaoke Hoster.exe.
- Choose Import, Import Karaoke Folders and select your karaoke folder.
- Choose Import, Import Music Folders if you want interval music.
- Press TV. The audience picture should appear full screen on Display 2.
- Choose Show, New Show when you are ready to start a fresh night.

## Add the first singer

### Add the first singer

- Click the singer-name box in the centre.
- Type the singer's name.
- Press Add Singer.
- Search in Karaoke mode for the requested artist or song.
- Drag the result onto the singer's row.

## Play the singer

### Play the singer

- Press Load Next Singer.
- Read the singer and song shown in the Karaoke Deck.
- Press the green Play button.
- Use yellow Pause if needed.
- Use red Fade Stop to lower karaoke for 1.5 seconds; returning music then fades in for 1.5 seconds.
- To load without a singer assignment, drag a Karaoke search result or karaoke file directly onto the Karaoke Deck, check it, then press Play.

## Keep the queue fair

### Keep the queue fair

- Drag a singer to change the order, or use Move Up and Move Down.
- Set As Next puts the selected singer first.
- Right-click and choose Hold when a singer has gone outside.
- Use Skip Once if they should be passed over only this time.

## Use a previous singer

### Use a previous singer

- Open the singer-name dropdown. It opens with recent singers instead of loading thousands at once.
- Type part of the name to search all saved singers.
- Select the name and add them to tonight's rotation.
- Open Songs and History to reuse one of their old songs.

## Run interval music

### Run interval music

- Drag Music search results to Deck 1 or Deck 2.
- Shift-click a range or Ctrl-click individual tracks, then drag the selection or use a deck button.
- Press Select All or Ctrl+A to queue every visible result from a folder search.
- Press the green Play button on a deck.
- Pause keeps the exact position; press Pause or Play to resume. Drag the timeline to seek.
- Import a music-video folder through Import, Import Music / Video Folders. Video tracks automatically appear full-screen and muted on the singer display while deck audio plays normally.
- Press Music Video at the top to search only the imported videos. Add or drag the result to a music deck, or press Space for quick play.
- Singer lists, scrollers, venue text, backgrounds and the logo hide automatically while the music video is on the singer display.
- Enable Auto Crossfade if you want the decks to alternate.
- For one-player operation, choose Show, Single Deck + Side List Mode. The right panel becomes a proper side list with Add, Load, Save, Select, Move, Send to Deck 1, Shuffle, Remove and Clear controls.
- Starting karaoke pauses the music plan; Play Music resumes the next scheduled track.
- In Music search, Space quickly plays the highlighted result and Space again stops it.

## Make the audience screen look good

### Make the audience screen look good

- Open Display, Overlay and Next Singer Settings.
- Choose whether to show the next four singers and their songs.
- Choose a background image, GIF or silent video, or choose a whole slideshow folder.
- Use Fit for the whole image, Fill for edge-to-edge cropping, or Stretch to fill everything.
- Move the singer scroller to the top if it blocks artwork at the bottom.

## If something goes wrong

### If something goes wrong

- No second-screen picture: press TV again and confirm Windows sees Display 2.
- Missing file: reconnect the drive, then rescan the watched folder.
- Red or broken song: choose another version; Hazz has marked the failed file so you can repair it later.
- Lyrics early or late: use the CDG minus or plus 0.25-second buttons.
- Wrong pitch: use Key minus or plus, then Reset when needed.
- Cramped controls: maximise Hazz or hide the preview.

## Finish safely

### Finish safely

- Let the current media stop.
- Close Hazz from the main window.
- Answer Yes to the shutdown question.
- Wait until Hazz closes before disconnecting a library drive or shutting down Windows.


# Virtual Folders — Beginner Walkthrough


## Virtual folders in plain English

A virtual folder is a list of shortcuts to your songs. It helps you group songs without moving or copying the actual files. Deleting a virtual folder does not delete your music or karaoke files.

![Virtual folder controls](Images/figure_virtual_folders.png)

## Make an 80s / Rock folder

1. Open **Library > Browse Library**.
2. Press **New Folder**, type `80s`, and create it.
3. Click **80s** in the left panel.
4. Press **New Subfolder**, type `Rock`, and create it.
5. Click **All Library Tracks**.
6. Choose **Music** and find the songs you want.
7. Hold **Ctrl** while clicking separate songs, or use **Shift** to select a range.
8. Press **Add Selected to Folder…**.
9. Choose **80s / Rock**, then press **Add Tracks**.
10. Click **Rock** in the left tree to see your collection.

## Bring folders over from BPM Studio

1. Close BPM Studio.
2. Choose **Import > Import BPM Studio** in Hazz.
3. Select the BPM Studio data/archive folder.
4. Confirm the import.
5. When it finishes, open **Library > Browse Library** and expand **BPM Studio**.

The source is read-only. Hazz creates links to the tracks and leaves every original file alone.

If several `.GRP` files have the same name, Hazz uses only the newest copy. Repeat imports skip files that have not changed. On a large library, let the progress window finish the **Linking**, **Saving** and **Finalizing** stages before starting another import.

## The safe buttons

- **Rename** changes the folder's label.
- **Remove from Folder** removes selected shortcuts from the open folder.
- **Empty** removes all direct shortcuts from the folder.
- **Delete Selected Folder** removes the folder and its child folders.

All four actions leave the physical song files and main Hazz library untouched.

## If it does not look right

- Click **All Library Tracks** to reset the view.
- Clear the search box.
- Check whether **Karaoke** or **Music** is selected.
- Open the child folder; the parent does not automatically show all child tracks.
- Maximise the Library Browser if a button is clipped.


Hazz Karaoke Hoster v0.90

Audience Display Settings now includes SHOW DURING MUSIC VIDEOS with independent options for:
- Permanent logo
- Rotation / venue scroller
- Next singers
- Kamikaze message

Check an option to retain that overlay during music videos. Its normal display settings must also be enabled. Leave all options unchecked for an unobstructed video, matching v0.80 behaviour. Changes apply live and are saved for the next launch. Background artwork remains hidden behind music videos.

All v0.80 features and fixes remain included.
