# Hazz Karaoke Hoster v1.74.8 — Complete broad search results and natural Disc sorting

v1.74.8 fixes a Karaoke search issue reported from live use.

## What was happening

Hazz's live search asked SQLite for only the first 300 relevance-ranked matches. If a broad search such as a maker/name returned more than 300 indexed tracks, clicking **Disc** only sorted that incomplete subset. A missing track could therefore appear when searched specifically even though it was absent from the broad list.

## Fixed

- Broad search no longer silently stops at 300 matches.
- 4+ character searches can display up to 20,000 matches; shorter very broad searches use lower safety limits to keep the host responsive.
- If a search ever reaches its safety ceiling, Hazz now says so in the status line instead of silently hiding that fact.
- Clicking **Disc** now uses natural numeric ordering, e.g. `Gnome 1, Gnome 2, Gnome 10, Gnome 100`.
- Disc ordering is reapplied after background duration probing refreshes the result rows.
- No files or database records are changed by this fix.

All v1.74.7 playlist deletion and earlier v1.74.x features remain included.
