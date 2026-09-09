# Venue profiles

## Singer lists — v0.98 test

**Automatic saving:** SAVE SINGER LIST or LOAD SINGER LIST activates that named venue. Hazz updates its singers, queue and history when explicitly saved, before changing singer lists, and on normal shutdown. The active venue is remembered on restart. Simply selecting a profile or applying its display settings does not change the active singer venue. The Venue Profiles status shows which venue is active. Automatic saving updates singer data, not the venue's display or audio settings.

**Without a venue:** singer names and permanent history still save automatically to the main database during normal use, and remain available next session. You do not need a venue profile to retain history. **USE WITHOUT VENUE** saves and disconnects the active named venue while keeping the current names, history and queue in the main database.

Starting blank or restoring a singer backup disconnects the current named venue after first saving it. Save or load a named singer list to activate automatic saving again. Clearing history or keeping selected singers changes the active venue too when it next saves. If automatic saving fails, Hazz reports it; if the final shutdown save fails, Hazz stays open so you can retry. There is no timed venue saving. Sudden power loss may leave the named snapshot at the previous explicit save or venue change; the working database still contains completed committed history entries.

Venue settings and singer lists have separate controls in this window. APPLY restores settings; LOAD SINGER LIST restores singers.

### Save a venue

1. Stop playback and open **Show > Venue Profiles**.
2. Enter a venue name and use **SAVE CURRENT** for display, sound and rotation settings.
3. Select that venue and choose **SAVE SINGER LIST**.

The singer snapshot includes every saved dropdown name, permanent song history, and the active queue with song choices, order and held status. Queued singers' group assignments are saved too. Once active, the venue updates automatically after subsequent shows. Saving settings alone preserves the existing saved singer list.

### Start another venue or keep regular followers

Save the venue you are leaving first. **START BLANK SINGER LIST** clears current names, history and queue after confirmation and backup. Create another venue with SAVE CURRENT and save its singers when ready.

Alternatively, choose **KEEP SELECTED SINGERS**. Ctrl-click individual names or Shift-click a range, choose **KEEP SELECTED — REMOVE OTHERS**, then confirm. Only selected names, their history and their active queue entries remain. Save that list to the new venue. Previously saved venue snapshots are unchanged. Choosing no names removes everyone, after confirmation.

### Load a venue

1. Stop playback and select the venue.
2. Click **APPLY** for its settings.
3. Leave **Load saved song history** ticked to restore history, or untick it to start permanent history fresh.
4. Click **LOAD SINGER LIST**, confirm, then check the queue and reload the next karaoke track.

Loading starts new show turn counts and does not start audio. Queued song choices are retained even with fresh history. Loading singers alone does not enable automatic rotation; APPLY restores the profile's saved rotation preference. Original manual rotation remains the default.

### Clear and recover

**CLEAR HISTORY ONLY** removes current permanent history while keeping names and queued songs. If a venue is active, this change updates that venue automatically. Other venues are unchanged.

Before changing singers, Hazz creates a singer-only backup. **RESTORE SINGER BACKUP** restores names/history from a selected backup and the queue where a matching .queue.json file exists. Without that sidecar the active queue starts empty. Restoration creates another backup first.

Singer snapshots live in **venue-singers**, with safety copies in **venue-singers/backups**, beside the main database. Back up **venue-profiles.json AND the entire venue-singers folder** to preserve named lists. The daily settings backup does not currently include this subfolder. Old snapshot revisions and safety copies are retained; review disk usage periodically. Deleting a profile does not delete retained singer snapshot files.

The shared media library, virtual folders and music playlists are not replaced. Do not delete/rename hazz-hoster.db just to clear singers: it also contains your indexed library. These controls operate only on singers and their history.

## Venue settings

Open **Show > Venue Profiles**.

1. Set up your audience logo, background or slideshow, scroller, music-video overlays and host layout.
2. Choose your sound devices and normalization setting. Set music volumes and crossfade options.
3. Open Venue Profiles, enter a venue name and choose **Save Current**.
4. At your next venue, stop all playback, select its profile and choose **Apply**. Reload any karaoke track that was already loaded.

Save Current asks before replacing an existing name. To copy a profile, apply it, type a different name and save. Rename changes only the selected profile's name. Delete removes the stored profile, not media, current settings, singers or history.

Profiles include audience artwork paths, text, colours, fonts, logo placement, overlay choices, host layout, audio endpoints, normalization, music volume and crossfade defaults. Artwork is referenced, not copied. Missing artwork is reported when applying. Display selection uses the saved available display index; check Display before opening fullscreen output, especially when hardware has changed.

Applying updates the current saved settings. Later adjustments do not automatically overwrite the named profile: use Save Current explicitly. Profiles are stored in venue-profiles.json beside the database and are included in subsequent daily settings backups.

Profile application does not start playback or change singer history or song files. Physical audio-device and multi-hour testing of the audio engine remains outstanding.

