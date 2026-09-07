# BPM Studio import — v0.6

The BPM Studio importer is intentionally **read-only**. Hazz does not write to, rename, move or delete BPM Studio data files.

## Import
Use **IMPORT BPM STUDIO** on the main toolbar and select the BPM Studio data/playlist folder.

The v0.6 importer looks recursively for:
- `.lst` — native list/history files;
- `.grp` / `.plg` — native archive/group/category files when they contain recoverable media paths;
- `.m3u` / `.m3u8`;
- `.pls`.

Native BPM Studio formats may contain proprietary/binary metadata. v0.6 recovers absolute/relative media paths and ordinary playlist records without inventing unknown fields. UTF-8, UTF-16 (including common no-BOM native files) and Latin-1 text are handled best-effort.

## What is stored in Hazz
- Imported playlist name and source path.
- Ordered playlist items.
- Music-history list name/order and the best available list date.
- Artist/title parsed from the referenced file name.
- Existing files are indexed/linked as **Music** records.
- Missing/moved references are retained in the imported playlist/history, allowing drive remapping/rescanning later without losing what BPM Studio contained.

Re-importing the same BPM source replaces that imported playlist/history source rather than multiplying duplicate rows.

## Using imported lists
Open **MUSIC LISTS / HISTORY**:
- choose a playlist to inspect all tracks;
- load the playlist to Deck 1 or Deck 2;
- select entries in Music History and load those files to either deck.

If old BPM Studio playlists point to a drive letter that no longer exists, those rows remain visible but cannot be played until the files are available at that path or a future remapping tool is used.

## Daily played-song history (v0.16.1)

BPM Studio dated played-song lists are now treated as daily music history even when they live in the ordinary Lists area rather than a folder named History. Hazz recognises common date-based list names, keeps the most complete physical copy for each calendar day, and imports that day in two useful forms:

- individual rows in **MUSIC HISTORY**, preserving the original play order; and
- a loadable **BPM Daily History — YYYY-MM-DD** list under **PLAYLISTS**, which can be sent to Deck 1 or Deck 2 as a whole.

Repeated songs are preserved. If the same track was genuinely played twice on the same day, both plays remain in the imported daily history. Extra backup/snapshot copies of the same dated BPM list are collapsed before database writes. BPM source files remain read-only.
