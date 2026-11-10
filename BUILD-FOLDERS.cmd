@echo off
setlocal
cd /d "%~dp0"
if not defined HAZZ_LIVE_STEMS_ENGINE set "HAZZ_LIVE_STEMS_ENGINE=%~dp0LiveStems"
if not exist "%HAZZ_LIVE_STEMS_ENGINE%\python\python.exe" goto MISSING_ENGINE
if not exist "%HAZZ_LIVE_STEMS_ENGINE%\hdemucs_high_trained.pt" goto MISSING_ENGINE
dotnet build "tests\LocalFilesChecks\LocalFilesChecks.csproj" -c Release -m:1 --disable-build-servers
if errorlevel 1 goto FAILED
dotnet "tests\LocalFilesChecks\bin\Release\net10.0-windows\LocalFilesChecks.dll" "%TEMP%\HazzFolderChecks-%RANDOM%-%RANDOM%"
if errorlevel 1 goto FAILED
dotnet build "tests\VideoResponsivenessChecks\VideoResponsivenessChecks.csproj" -c Release -m:1 --disable-build-servers
if errorlevel 1 goto FAILED
dotnet "tests\VideoResponsivenessChecks\bin\Release\net10.0-windows\win-x64\VideoResponsivenessChecks.dll" "tests\VideoResponsivenessChecks\Fixtures\video.mp4" "tests\VideoResponsivenessChecks\Fixtures\audio.wav"
if errorlevel 1 goto FAILED

dotnet publish "src\HazzKaraokeHoster.App\HazzKaraokeHoster.App.csproj" -c Release -r win-x64 --self-contained true -m:1 --disable-build-servers -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o "RELEASE\HazzKaraokeHoster_v2.30_Folders"
if errorlevel 1 goto FAILED
xcopy "Documentation\Templates\*" "RELEASE\HazzKaraokeHoster_v2.30_Folders\Templates\" /E /I /Y >nul
if errorlevel 1 goto FAILED
xcopy "%HAZZ_LIVE_STEMS_ENGINE%\*" "RELEASE\HazzKaraokeHoster_v2.30_Folders\LiveStems\" /E /I /Y >nul
if errorlevel 1 goto FAILED
copy /Y "tools\LiveStems\live_stems.py" "RELEASE\HazzKaraokeHoster_v2.30_Folders\LiveStems\live_stems.py" >nul
if errorlevel 1 goto FAILED
echo Ready: RELEASE\HazzKaraokeHoster_v2.30_Folders\Hazz Karaoke Hoster.exe
pause
exit /b 0
:MISSING_ENGINE
echo Put the engine in the source LiveStems folder or set HAZZ_LIVE_STEMS_ENGINE to its location.
:FAILED
echo Build or checks failed.
pause
exit /b 1
