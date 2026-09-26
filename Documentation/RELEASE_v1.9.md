# Hazz Karaoke Hoster v1.9 — Library and playback tools

Made For KJ/DJ's By A KJ/DJ

## Library Health Centre

1. Open **LIBRARY → Library Health Centre**.
2. Press **SCAN LIBRARY**. The scan runs in the background and reports progress. It checks files already indexed in Hazz, not every file on your drives. A full ZIP scan reads the contents, so a large collection can take time. Run it before or after your show. **CANCEL SCAN** stops the check without changing files.
3. Read the findings. Use the column headers and horizontal scroll bar to inspect paths and details.

Findings explained:

- **Missing file:** the file is missing, inaccessible or its drive is disconnected. Connect the drive before deciding to remove an entry.
- **Unreadable / broken file:** the file cannot be read, or ZIP decompression/checksum validation failed.
- **Missing audio partner / Missing CDG partner:** the matching same-name file is absent. Ordinary Music tracks do not need CDG graphics. An audio-only track deliberately imported as Karaoke can be left unchanged.
- **Missing ZIP partner / ZIP partner name mismatch:** the ZIP lacks audio or graphics, or their names differ. Inspect the original archive and restore the correct matching files from your own good copy.
- **Possible duplicate:** another entry has the same artist/title. It can still be a different manufacturer, live recording or arrangement. Hazz never chooses which version to delete automatically.
- **Duplicate path:** another database entry points to the same path, ignoring letter case.
- **ZIP not checked:** archives above 2 GB uncompressed or 10,000 entries need manual inspection. They are not labelled healthy.

The scan does not rewrite ZIPs, invent missing graphics, decode every audio/video file or repair damaged CDG tiles. A clean scan confirms these file/structure checks, not that every recording sounds or looks correct.

### Reconnect after a drive letter or folder changes

Example: the library previously lived at `E:\Karaoke` and is now at `F:\Karaoke`.

1. Complete a scan.
2. In **Old folder / drive**, type `E:\Karaoke`.
3. In **New folder / drive**, type `F:\Karaoke`.
4. Press **PREVIEW RECONNECT**. Hazz preserves the relative subfolder and filename. It proposes only missing entries whose target exists and whose recorded file size matches (when a size was recorded). Nothing is saved yet.
5. Inspect the old and new paths. A size match is not proof that two recordings are identical: check that you chose the right root. Renamed files and changed-size files need manual investigation.
6. Stop the players, then press **CONFIRM RECONNECT** and confirm the reviewed batch. All displayed proposals are applied together.
7. Scan again and reload any previously loaded deck track before playing it.

A failed database batch rolls back completely. Conflicting existing library paths are not silently merged. Song IDs, virtual-folder membership, key/sync values and database history remain; history and saved database playlist paths are updated. Current singer and music queue paths are updated. Favourites, saved tempo and per-song display/sync settings are copied to the new paths while retaining their old entries.

Separate saved venue snapshots and external playlist files keep their original paths. Reconnect after loading an old venue if necessary. Update/import your watched library folders separately when moving the library; reconnecting recordings does not rewrite watched-folder settings.

### Remove unwanted entries

1. In scan findings, select the unwanted rows. Ctrl-click selects individual rows; Shift-click selects a range.
2. Press **REMOVE SELECTED DATABASE ENTRIES**.
3. Read the confirmation and choose Yes only for the entries you want removed.

This removes library and virtual-folder references, not media files. History text is retained. Different versions can share an artist and title, so compare their paths before removing them. After viewing reconnect proposals, scan again to return to findings.

## Better Find Alternative

1. Load the karaoke song you want to replace. To work with a queued singer request, load that request first using Load Next Singer or drag it from the singer's song window.
2. Stop any karaoke performance before changing versions.
3. Press **FIND ALTERNATIVE**.
4. Compare the current recording at the top with the list below. Artist, title, manufacturer, disc, duration (when known) and file path are shown.
5. Select a recording and press **REPLACE LOADED / QUEUED VERSION (DO NOT PLAY)**.
6. Press the normal karaoke **PLAY** button when ready.

Search considers words regardless of their order, accent/punctuation differences, swapped artist/title fields and some extra version words. For example, `John, Elton` can match `Elton John`; an entry with `Your Song` in Artist and `Elton John` in Title can also match. Suggestions are ranked, not automatically accepted. Up to 300 suggestions are displayed from a bounded candidate search. This is not a guarantee that every spelling error or arrangement will match.

Replacing a queued request retains its identity, position, singer's key and sync adjustment, and the singer's rotation position. It updates the recording metadata and duration. Loading alone does not consume the request or write a performance to history. Different recordings can have different intros, so check the retained sync before the show.

## Drag a singer's song to the karaoke deck

1. Double-click a singer to open their song list. The main console remains usable.
2. Drag the desired row from their queued songs onto the central karaoke deck.
3. It replaces the loaded track and retains that request's singer, key and sync. The request remains queued until playback starts.
4. Press **PLAY** when the singer is ready.

Stop active/paused karaoke first; dropping a request cannot interrupt an ongoing performance. Dropping does not move the singer in the rotation. The previously queued song is not deleted merely because you loaded another choice. This action uses the queued-song list, not the history list.

## Automatic loudness matching

1. Press **NORMALIZE AUDIO** at the top of the console.
2. Tick **AUTOMATIC LOUDNESS MATCHING — KARAOKE + MUSIC**.
3. Start with the recommended target of **−18 dB RMS**. Move the target towards −24 for quieter output or −12 for louder output.
4. Set **Maximum boost for quiet tracks** between **0 and +12 dB**. At 0, loud material is reduced but quiet material is not boosted. A lower cap reduces the amount of background noise that may be raised.
5. Press **SAVE AND CLOSE**. Venue profiles also retain the boost limit.

The existing shared leveller covers both music decks, quick-play and karaoke audio. v1.9 adds the adjustable boost limit and holds gain through near-silent passages instead of drifting back towards unity. Changes are gradual; deck volumes, fades and sound-FX ducking still apply. Original files are unchanged and the option remains opt-in, preserving your previous enabled setting.

This is real-time RMS matching with a per-player peak limiter, not an offline LUFS analysis or a guarantee of identical perceived loudness. It takes time to settle, and very quiet tracks can remain quieter when the boost cap is reached. It does not normalize microphones, external applications or the separate sound-FX pads. Multiple outputs mixed together still need sensible mixer levels.

If a karaoke file is using the fallback audio path, stop and reload it before enabling matching. If the selected audio path cannot decode a file, Hazz reports that rather than silently bypassing the requested processing.

## Installation and testing

Extract the portable ZIP into a new folder and keep all included files together. Alternatively use the optional single-EXE ZIP. Existing application data remains in the normal Hazz data folder.

Source users: open `HazzKaraokeHoster.sln` in Visual Studio 2026 with the .NET 10 desktop workload, restore packages, select Release and Build Solution. Outputs are in `RELEASE/HazzKaraokeHoster` and `RELEASE/SingleFile`.

Focused regression checks use temporary data, including ZIP CRC damage, missing pairs, candidate matching, transactional reconnect rollback, retained request settings and generated audio. Actual hardware routing, interactive drag/drop on your setup and an hours-long show still need rehearsal.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
