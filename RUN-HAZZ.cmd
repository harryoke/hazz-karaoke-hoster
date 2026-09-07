@echo off
setlocal
cd /d "%~dp0"
set "APP=%CD%\RELEASE\HazzKaraokeHoster\Hazz Karaoke Hoster.exe"
if not exist "%APP%" (
  echo The Release EXE has not been built yet.
  echo Run BUILD-EXE.cmd first. A normal Visual Studio Release build validates the source but does not publish the standalone EXE.
  pause
  exit /b 1
)
start "" "%APP%"
