# Visual Studio 2026 build — Hazz Karaoke Hoster v0.80

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

A Release/x64 build automatically publishes a self-contained single-file build to:

`RELEASE\HazzKaraokeHoster\Hazz Karaoke Hoster.exe`

You can also double-click `BUILD-EXE.cmd` from the solution root.

The application database is created under:

`%LOCALAPPDATA%\Hazz Karaoke Hoster\hazz-hoster.db`
