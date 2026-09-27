# Hazz Karaoke Hoster v2.01

Made For KJ/DJ's By A KJ/DJ

This release adds a Singer Database Manager, five music crossfade presets and faster Library Health scanning. All v2.0 skins and existing playback features remain included.

## Singer Database Manager

1. Open **SHOW → Singer Database Manager**.
2. Choose **Current saved singers** or a named venue from the top list. The active venue is represented by the current list, so you cannot accidentally edit an older copy of it. Create venue profiles in SHOW → Venue Profiles first if needed.
3. Type part of a singer name and press Enter or SEARCH. Clear the search to see everyone. Ctrl-click or Shift-click selects multiple singers. The Performances column totals their saved song counts.
4. Choose an action below. Every destructive action asks for confirmation. Karaoke must be stopped before edits.

**VIEW / DELETE HISTORY:** Select one singer. Their saved performances appear in a separate window. Select individual entries and press DELETE SELECTED HISTORY ENTRIES, then confirm. Other entries and the singer name remain.

**CLEAR SELECTED SINGERS’ HISTORY:** Select one or more singers to clear all their history in this list, while keeping the saved names and tonight’s rotation.

**DELETE SAVED SINGERS:** Deletes the selected names and their histories from this list. This is permanent data management, not removal from tonight. Other venue lists are unaffected.

**MERGE DUPLICATES:** Select two or more names belonging to the same person, press MERGE DUPLICATES, choose which existing name to keep, then confirm. The histories and notes are combined, including song key and sync values. Individual performances are retained rather than guessed to be duplicates. A keeper’s existing photo is preserved; otherwise a selected singer’s photo can be copied. The program does not automatically decide that similar names are the same person.

**COPY TO LIST:** Select singers, choose a different destination, and click COPY TO LIST. Their names and histories are added there. Existing matching names are combined; their existing notes/photo are retained. Repeating an unchanged copy does not duplicate matching history entries. Tonight’s queued songs are not copied.

**MOVE TO LIST:** Works like Copy, then removes the source saved names and histories only after the destination is saved. If source removal fails, the destination copy remains safe; you may have singers in both lists and can retry. A move is not one atomic transaction across venue files.

Photos are copied when available without overwriting a destination photo. A photo-copy failure is reported separately from saved singer data. Original photo files and older backups are not deleted.

To merge, move out or delete a current saved singer who is in tonight’s rotation, first remove them from the main singer list. This avoids changing the identity of an already queued request. **The main singer list’s REMOVE button removes them from tonight only and keeps permanent history.** The existing protection for a loaded/playing singer remains.

Manager edits are explicit saves. Current changes update the main database and the active venue; saved-venue edits create a new singer snapshot. No timed saving has been added. Singer-only recovery copies are stored in the database folder under `venue-singers/backups`. Existing Venue Profiles → RESTORE SINGER BACKUP can restore one, replacing the current list and queue after confirmation. Other saved snapshots are not erased when deleting history. No media files or music-library entries are removed by this manager.

## Music crossfade presets

1. Find **CURVE** beside FADE NOW in the music crossfade controls.
2. Select a preset. The graph shows outgoing volume in orange and incoming volume in blue.
3. Press **USE THIS PRESET**.
4. Set the existing TIME slider, then use AUTO CROSSFADE or FADE NOW.

- **Linear:** the original straight fade; still the default.
- **Equal power:** curved gains intended to maintain more energy through the centre.
- **Smooth:** gradual start and finish, with an S-shaped change.
- **Overlap:** brings the next track up early while retaining the outgoing track longer.
- **Fade out then in:** finishes fading out before fading the next track in, with silence at the midpoint.

These affect two-deck music transitions only. Single-deck playback and karaoke duck/pause/resume fades retain their existing behaviour. A running fade keeps the curve it started with; a newly chosen curve applies to the next fade. The preference is saved with the normal console settings and venue setup. Equal-power and overlap fades can sound louder in the middle; leave suitable mixer headroom.

## Faster Library Health Centre

Open LIBRARY → Library Health Centre and leave **Deep ZIP verification** unticked for a normal scan. Quick scanning checks missing/empty files, ZIP structure, partners and possible duplicates without decompressing every song. Progress shows the file being checked.

Enable **Deep ZIP verification** when you want ZIP payload checksum checks as well. This reads/decompresses archive contents and can take much longer. Quick scanning cannot detect all payload corruption. Existing safety limits and cancellation remain. Slow Windows drive access can delay cancellation.

A synthetic one-million-entry check completed in 41.27 seconds with steady progress beyond 163,000 entries. This tests database processing with missing local paths, not the time needed to read one million real files from your drive.

## Installation and validation

Close Hazz, extract the complete Windows ZIP into a new folder and run Hazz Karaoke Hoster.exe. An optional single-EXE ZIP and complete Visual Studio 2026 source are provided. The interface is labelled v2.01; the numeric assembly version is 2.1.0. Existing library, skins and settings are retained.

Automated checks cover saved-name search, merge/history preservation, copying both directions, repeated copies, deletion, rollback, move ordering and all five curve shapes. Release build and package dependency checks are included. Physical mixer output, external TV behaviour and a complete live-show rehearsal are not verified by these automated tests. Existing PDF manuals remain older base guides; this document supplies the new instructions.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
