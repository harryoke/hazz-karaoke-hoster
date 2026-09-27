# Singer-list column widths — v2.01 TEST3

Made For KJ/DJ's By A KJ/DJ

The main karaoke singer list now remembers resized columns when you close Hazz normally and restores them at the next start. This includes your Deck 1 / Karaoke Deck / Singer List arrangement.

1. Extract the full test ZIP into a new folder and close the older Hazz instance.
2. Start Hazz Karaoke Hoster.exe. The version label is v2.01 TEST3.
3. Drag the edges between column headings in the main singer list to the sizes you prefer. Narrow columns stay narrow, within the existing minimum widths.
4. Close Hazz normally, then reopen this same build. The widths should return without having to adjust them again.

Column widths also form part of venue setup when you explicitly use SAVE CURRENT in Venue Profiles. Applying a saved venue setup restores its stored widths. Older profiles without column settings leave existing widths unchanged. All displayed column widths are saved as fixed sizes, including columns that previously sized themselves automatically. This prevents startup content from redistributing the widths. On a smaller window, use horizontal scrolling rather than expecting these saved columns to shrink.

TEST3 corrects TEST2: the earlier build saved automatic/proportional sizing rules for some columns, so they could shift on restart. Set your preferred widths once in TEST3, close normally and reopen. Exact sizes are retained at the same Windows display scaling.

This change stores column widths only, not a separate width layout for every skin or display mode. It does not delete or change singer histories, favourites or audience-screen preferences. A forced termination before normal closing may lose unsaved layout adjustments.

This test build also includes the unpublished music effects (flanger, reverb and echo). See MUSIC_EFFECTS_TEST.md in the source documentation. Public GitHub/web remain v2.01.

Automated layout checks verify save/JSON/reload of wide and narrow columns, resolved pixel boundaries despite different startup text and a narrower viewport and safe handling of invalid saved values, plus all skins and console modes. Reopening with your own saved settings still needs your confirmation.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
