# Importing libraries from other hosting / DJ software

Hazz v0.14 uses a read-only adapter layer so third-party databases can be migrated without allowing Hazz to write into the original application's files.

## VirtualDJ
Use **IMPORT > Import Other Software > VirtualDJ database.xml...** and select VirtualDJ's `database.xml`. Hazz streams Song records, reads file path and tag metadata where present, previews a sample, then copies selected records into Hazz's SQLite library.

## Generic imports
Use **Other hoster database / playlist...** for XML, M3U/M3U8, PLS/LST, CSV/TSV/TXT, SQLite DB, MDB/ACCDB/KDB.

For unknown database schemas, Hazz searches read-only for a table containing a plausible media/file-path column and maps common Artist/Title/Manufacturer/Disc field names. If a usable mapping cannot be found, import stops rather than inventing a mapping.

## Karaoke vs Music
The preview window allows:
- **AUTO**: CDG/ZIP and karaoke-hinted paths are Karaoke; ordinary audio is Music; videos use path/type hints.
- **KARAOKE**: force every supported media record into the Karaoke library.
- **MUSIC**: force every supported media record into the Music library.

## File verification
Fast import trusts existing stored paths. **Verify files exist** checks each path and is slower on very large collections.

## After migration
Hazz infers sensible library roots from imported paths and adds them to its watched roots. New files copied to those drives/folders can then be indexed by Hazz without rebuilding the imported library. Use **LIBRARY > Rescan Watched Folders...** for files added while Hazz was closed.
## Virtual-folder organisation

Smart Import also preserves folder and crate organisation when the source exposes it in a readable format. VirtualDJ 2024 **My Lists** (`.vdjfolder`/XML), older VirtualDJ M3U/PLS playlist trees, Rekordbox XML playlist trees, and nested M3U/M3U8/PLS/XSPF/WPL exports are imported below a Hazz folder named after the detected application. Each list is a link collection; Hazz does not move or copy the media files.

BPM Studio uses its dedicated importer. In v0.80 it skips unchanged `.GRP` and `.PLG` files, chooses only the newest copy when duplicate filenames exist, and batches large song indexes and virtual-folder links so the final import stages remain visible and cancellable.

For VirtualDJ, choose the complete application home folder so Hazz can see `database.xml` together with `MyLists`, `Playlists`, or `Folders`. Close the source program first. Repeating the import reuses matching Hazz folders and track links. Proprietary crate files that do not expose recoverable paths are left untouched and listed in the import report.
