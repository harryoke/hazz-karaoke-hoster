# Hazz Karaoke Hoster v1.74.1 — Search Columns restored

v1.74.1 is a patch release built on the complete v1.74 source. It restores the Search Columns feature from the earlier unpublished Search Columns test build without rolling back any v1.73/v1.74 work.

## Search-result columns

Press **COLUMNS** in the search-results header to choose which columns are shown. At least one column always remains visible.

- Drag column edges to resize.
- Drag headings to reorder.
- Visibility, width and order are remembered separately for **Karaoke**, **Music** and **Music Video**.
- **Music Length** remains hidden by default and can be enabled from COLUMNS.
- Use **Reset this search mode to defaults** to reset only the active mode.

Settings are stored in `search-columns.json` beside Hazz's `hazz-hoster.db` under the Hazz Karaoke Hoster local application-data folder. The previous readable settings copy is retained. Existing Search Columns test settings are read where compatible.

All v1.74 Music Video imports/library separation, Side List virtual folders, Fit/Stretch, expanded transitions, search-result reopen-on-focus fix, consecutive-video fix and LibVLC build fix remain included.
