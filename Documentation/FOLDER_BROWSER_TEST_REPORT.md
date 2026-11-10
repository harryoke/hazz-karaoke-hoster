# v2.20.1 FOLDERS validation — 23 October 2026

Built from the original user-supplied Hazz_Karaoke_Hoster_v2.20_VS2026_Source.zip into a separate source directory. Every original file is retained. All Core and Playback source files match the original ZIP byte for byte. No AI folder, Smart Automix files, stem code, models or new package dependencies were imported.

- **23 focused checks passed**, covering folder enumeration/sorting, supported formats, filtering, cancellation, missing paths, standard Windows file-drop payloads, production queue insertion, playing-row protection, skipped/missing files, repeat entries, second-deck queues and oversized-drop rejection.
- Production browser window checks passed for asynchronous file listing, lazy tree expansion, stale-navigation suppression, failed-navigation clearing, filter control and cancellation on closing. Large-folder tests verify the 5,000-row cap and finding matches beyond that cap.
- Music-drop insertion code is exercised in an isolated WPF host using the actual production helper. No user database, music routes or player state are opened by these tests. The physical mouse-drag gesture and complete MainWindow live-show workflow were not exercised end to end.
- Host layout passed at 1920x1080, 1366x768, 1024x768, 800x600 and 853x480: 63 buttons checked per size. The existing layout-check harness was repaired to resolve app XAML types and to return a failure exit code.
- Browser screenshot rendered and visually checked. A resizable modeless window keeps the main console available; standard file-drop payloads allow dragging from this browser or Windows Explorer.
- Self-contained Windows x64 publish passed. Published product version: 2.20.1. Package check passed: bundled VLC runtime/plugins and SQLite load.
- Existing karaoke drop loading and its active/paused-performance guard remain unchanged from v2.20. Music drops queue files without directly starting playback; existing automation behavior is retained.

Folders and files are read on background tasks. Access/missing-drive errors are handled, but physical USB removal, locked optical drives and every Windows permission configuration were not tested. Underlying OS I/O may finish after cancellation; stale results are discarded. The tree limits each expansion to 2,000 children and displays a path-entry hint; file filtering covers the whole current folder with up to 5,000 visible matches. Drops are limited to 500 files.

Application build/publish succeeded without compiler errors. The isolated test harness emits one nullable-cast warning for its asserted file-drop payload; production build is clean. This is not a dependency security audit. No live-show soak was performed.
