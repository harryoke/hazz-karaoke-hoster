# Library Health Centre speed fix — v2.0 test build

Made For KJ/DJ's By A KJ/DJ

Extract the entire Windows ZIP to a new folder and run Hazz Karaoke Hoster.exe. Close your previous copy first. This test build retains v2.0 features and settings; it has not been published as a new release.

Open Library Health Centre and leave **Deep ZIP verification** unticked for the normal scan. Click **SCAN LIBRARY**. This checks missing files, empty files, ZIP structure, MP3/CDG partners and possible duplicates without decompressing every song. Progress shows the current file and checked count. The scan itself does not change your library or files.

Tick **Deep ZIP verification** before scanning when you want to check compressed contents for damage. This reads and decompresses ZIP entries and checks their CRC checksums, so it can take much longer on large libraries. Quick scanning cannot detect every damaged payload. Safety limits still apply to unusually large archives. Cancel stops at the next available cancellation check; Windows drive access can still delay cancellation.

Validation: one million synthetic database entries with missing local paths scanned in 41.27 seconds, with one million findings retained and 606 MB peak process memory. Progress remained steady beyond 163,000 entries. This is not a timing estimate for one million actual files or ZIPs. Your drive's real-world scan speed remains to be tested.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
