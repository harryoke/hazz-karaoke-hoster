# Karma migration design
Hazz imports from Karma in read-only mode.

Supported discovery in v0.1:
- Current/relational `.kdb`, `.mdb`, `.accdb` through Microsoft ACE OLE DB.
- Legacy `datamain.xml` / XML layout.
- Karaosoft Data folder detection is represented in the service API; the UI currently selects a file so a specific source can be audited before import.

Safety rules:
- Never write to the Karma source.
- Hazz creates its own singer records.
- Unknown table schemas are not guessed aggressively.
- An import log is written to Hazz's SQLite database.

Next step for exact singer-history migration:
Use a copy of a real Karma KDB from the user's installation to map the exact table/column relationships. This is intentionally safer than assuming table names across Karma versions.


## v0.13 complete karaoke-library import
Use **IMPORT > Import Karma > Complete Karaoke Library** to migrate an existing Karma media catalogue without rebuilding it from the hard drive. Hazz opens the KDB/Access database read-only, inspects table/column names and sample values, previews the detected mapping, then imports into Hazz's own SQLite database.

Fast mode does not call `File.Exists` for every track, which is the intended path for very large libraries. Verify mode checks each path and counts missing files. The original Karma database is never modified.

After migration, Hazz infers top-level karaoke folders from the stored Karma file locations and saves them as library roots. While Hazz is running, those roots are monitored for newly copied/renamed files. Use **LIBRARY > Rescan Watched Folders** for files added while Hazz was closed or whenever Windows reports a watcher overflow.


## v0.15.4 singer-history mapping
Hazz now resolves relational Karma singer/song foreign keys as well as direct name/title/path fields. Where Karma stores an aggregate Times Sung/play count, Hazz preserves that number. The Singer Songs & History window shows Last Sung and Times Sung and allows a prior song to be dragged or double-clicked back into the active song list. Karma files are always opened read-only.
