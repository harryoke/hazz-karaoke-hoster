# Hazz Karaoke Hoster v1.0

## New since v0.95

- Venue profiles for display, sound, layout and rotation preferences.
- Separate venue singer lists and permanent history, with load/save, fresh-history, selected-regulars, clear and backup-restore controls.
- Active venue singers save on request, before changing singer lists and on normal shutdown. There is no timed venue-save interval. Without a venue, history still saves in the main database.
- Optional rotation methods: Round robin, Closed rounds, Newcomers at the end, Interleaved newcomers, Longest waiting, Fewest turns, Group rotation and Arrival order. Original manual rotation remains the default.
- Reduced repeated interface updates and next-track checks.
- Corrected shutdown sequencing for immediately completed venue saves.
- Compressed automatic database backups: at most three generated ZIPs within a 1 GB budget, always retaining the newest backup. Legacy uncompressed backups remain untouched.

Includes the existing separate sound outputs, audio normalization, music-video overlays, single-deck side list, standby track checks, virtual folders and read-only imports.

## Downloads

Extract the portable ZIP and run Hazz Karaoke Hoster.exe. The source ZIP includes the self-contained executable under RELEASE and the Visual Studio 2026 solution.

The fully revised illustrated manual is being completed separately. This release includes current feature guides; older website manual pages are labelled accordingly.

## Validation and limits

Release publish succeeded. Singer snapshot, automatic-save, backup compression/retention and shutdown checks passed in development. Current console layout checks passed at five sizes. ZIPs are read-tested and accompanied by SHA-256 hashes. Physical multi-device and extended live-show testing remain equipment-dependent.

Back up your database before upgrading. Venue singer snapshots require copying venue-profiles.json with the venue-singers folder; the automatic database ZIP does not include that subfolder. Saving or clearing an active venue changes its saved list. Use USE WITHOUT VENUE before retaining selected followers if you want to preserve the old venue unchanged.
