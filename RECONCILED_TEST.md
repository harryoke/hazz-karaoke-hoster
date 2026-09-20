Historical development notes. Current release: v1.6; use Documentation/RELEASE_v1.6.md.

# Hazz Karaoke Hoster v1.4.2 — Reconciled Test

This unpublished test combines the newer v1.4.2 BuildFix6 source with the CD+G sync correction documented for BuildFix7, plus the skins, optional single EXE and data-protection work. The BuildFix7 archive was not available; its sync correction was reapplied and tested.

## Build and run
Open HazzKaraokeHoster.sln in Visual Studio 2026 with the .NET 10 desktop workload. Select Release and Build Solution. The standard app is in RELEASE/HazzKaraokeHoster. Keep that whole folder together. The optional self-contained EXE is in RELEASE/SingleFile. BUILD-EXE.cmd and BUILD-SINGLE-EXE.cmd build the formats individually.
The single EXE extracts bundled runtime files automatically. Both formats use the existing Hazz data under Local AppData; they are not separate venue databases.

## Skins and display modes
Choose DISPLAY > Console Skin, then Classic, Midnight, Copper or Daylight. Classic retains the original layout. The other layouts rearrange the existing controls; their functions remain the same. DISPLAY also provides Karaoke Only, Single Deck + Side List, and Karaoke Focus. Karaoke Focus uses one music deck and moves singers into the space normally used by Deck 2. The skin determines where that space sits. Leaving Focus retains Single Deck until you turn it off.

## Dragging beyond the visible rows
Keep holding the mouse button and move the dragged song or singer near the top or bottom of the list. The list scrolls until you move back into the middle, release the button or cancel with Escape. You do not need to release and drag again for each screen of rows. This applies to Deck 1, Deck 2/the side list, the main singer rotation, a singer's queued songs and the virtual-folder tree. It scrolls the list's existing contents; it does not change the library browser's separate search-page selection.

## Newer features retained
- Karaoke progress slider and minus/plus five-second buttons move through the loaded track. CD+G redraws at the selected point.
- CD+G sync: positive values advance graphics, negative values delay them; steps are 0.25 seconds. Existing saved numbers are retained.
- Karaoke search shows track length when available. It reads up to 80 uncached results in the background. Queue REMAINING includes queued requests and the current song remainder; held singers are excluded and unknown lengths are estimated at four minutes (marked ~). These are estimates, not guaranteed show finish times.
- Singer history: open the singer's history, filter if desired and choose EXPORT CSV to export the displayed rows.
- Search supports punctuation. Right-click a result to favourite it or remove its database entry. Removal does not delete the media file or singer history. Missing-result removal affects only the displayed results.
- ZPB catalogue names are recognised and the known old parsing error is repaired on database initialization.
- Karaoke Focus resumes music on Deck 1. Both deck LED messages use their own scrolling timer.

## Protection and corrections
Changing skin saves the skin choice independently of audience controls. Settings preserve unknown options, keep bounded recovery copies and refuse to overwrite unreadable settings with defaults. Startup guards protect music queues and show recovery before initialization completes. Venue snapshots are left intact if show recovery fails. Favourites remain separate from skin settings.
Search now rejects superseded results, and a duration refresh cannot restore entries just removed from results. Queue length lookups use a stable snapshot while the host edits the queue. Re-entering Single Deck through Focus preserves the previous crossfade preference.

## Validation and testing
Build and focused automated checks cover punctuation, filenames, database deletion/history preservation, duration caching, CD+G sync/transparency, music takeover modes, settings preservation, early shutdown, and skin layout/control retention at three display widths and two font sizes. Validation uses temporary data, not the live show database.
Before using this test at a show, check music-to-karaoke handoff in each mode, pause/seek with Windows and VLC, audience output, venue switching, saved settings after restart, and extended playback on the actual laptop. No GitHub release has been published from this reconciliation.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
