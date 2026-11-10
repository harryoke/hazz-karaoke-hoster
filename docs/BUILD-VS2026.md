# Visual Studio 2026 build — Hazz Karaoke Hoster v2.30

1. Install Visual Studio 2026 with **.NET Desktop Development**.
2. Ensure the .NET 10 SDK is installed.
3. Open `HazzKaraokeHoster.sln`.
4. Allow NuGet restore for:
   - Microsoft.Data.Sqlite 10.0.11
   - System.Data.OleDb 10.0.11
   - NAudio 2.2.1
5. Select **x64** and Debug or Release.
6. Build Solution.
7. Set `HazzKaraokeHoster.App` as startup project if Visual Studio has not done so automatically.

For the complete Windows portable build, copy the released `LiveStems` directory into the source root (or set `HAZZ_LIVE_STEMS_ENGINE` to its location) and run `BUILD-FOLDERS.cmd`. It runs the folder and playback checks and places the app, engine and templates together under `RELEASE`. An IDE build does not bundle the engine automatically.

The application database is created under:

`%LOCALAPPDATA%\Hazz Karaoke Hoster\hazz-hoster.db`
