> Included in v2.30. See the [v2.30 release notes](RELEASE_v2.30.md) for current installation instructions.

# Hazz v2.20.2 FOLDERS — local drive browser

This build starts from the original v2.20 source ZIP. It adds local file browsing and drag-and-drop, independently of the Automix/stems fork. No AI engine or models are required.

## Use

1. Extract the complete Windows ZIP into a new folder. Run **Hazz Karaoke Hoster.exe** and confirm **v2.20.2 FOLDERS**.
2. Click **FOLDERS** next to the search field, or **LIBRARY → Browse Local Drives / Folders…**.
3. Expand a local/removable drive and its folder tree. Selecting a folder lists its supported files. You can also enter a folder path and press Enter or GO; UP and REFRESH are available.
4. Type part of a filename into **Filter filenames…** to narrow the current folder. The filter does not search subfolders.
5. Select one file, or use Ctrl/Shift for several, then drag:
   - **Music deck 1 or 2 playlist:** copy files into the queue at the drop position. A playing row stays ahead of inserted files.
   - **Music player panel:** append files to that deck's queue.
   - **Karaoke deck:** load the first selected supported file through the existing v2.20 karaoke loader. Stop the current karaoke song first; an active/paused karaoke performance is protected.
6. Move or resize the browser beside the decks, or bring the host in front during a drag. It is a separate, nonmodal window, so the host remains usable. The FOLDERS button brings the same browser back to the front.

Music decks accept supported audio and video formats. Karaoke ZIP/CDG files belong on the karaoke deck. MP3/CDG pairs are displayed as separate files; dragging the CDG or matching audio file to karaoke lets the existing loader find its companion. ZIPs must contain a valid karaoke package. Unsupported or disappeared files are skipped when dropping onto music decks, with a status message. Drops copy queue entries; they do not move/delete source files or directly start playback. Existing v2.20 automation settings still govern queued music.

Browsing does not require a database import or a recursive drive scan. Hidden/system folders are omitted from the tree. Folder children load when expanded; file listing and drop validation run off the UI thread. Unavailable/permission-denied folders show a message. Mapped network-drive roots are omitted; a folder path can be entered directly.

For predictable resource use, up to 5,000 matching files are displayed per folder and 500 files can be dragged per operation. The match count is shown if the list is capped, and filename filtering searches the whole current folder so files beyond the display cap remain reachable. A tree expansion shows up to 2,000 immediate child folders; enter a path directly to reach an omitted folder. Normal browsing is read-only.

## Source/build

Use the included **BUILD-FOLDERS.cmd** with a .NET 10 SDK / compatible Visual Studio installation. It runs the focused checks and publishes a self-contained Windows x64 release. The source is maintained separately from the v2.21–v2.25 AI work. Its playback implementation remains the original v2.20 code.
