@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo HAZZ KARAOKE HOSTER v1.4.2 - USER FEEDBACK TEST BUILD
echo ============================================================
echo.
echo [1/4] Running punctuation-search regression check...
dotnet run --project "tests\SearchPunctuationChecks\SearchPunctuationChecks.csproj" -c Release
if errorlevel 1 goto FAILED

echo.
echo [2/4] Running karaoke filename parsing regression check...
dotnet run --project "tests\KaraokeFilenameChecks\KaraokeFilenameChecks.csproj" -c Release
if errorlevel 1 goto FAILED

echo.
echo [3/4] Running stale library-entry deletion regression check...
dotnet run --project "tests\LibraryDeleteChecks\LibraryDeleteChecks.csproj" -c Release
if errorlevel 1 goto FAILED

echo.
echo [4/4] Publishing Windows x64 portable test...
dotnet publish "src\HazzKaraokeHoster.App\HazzKaraokeHoster.App.csproj" ^
  -c Release ^
  -r win-x64 ^
  -m:1 ^
  --disable-build-servers ^
  --self-contained true ^
  -p:PublishSingleFile=false ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:PublishReadyToRun=false ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "RELEASE\HazzKaraokeHoster_v1.4.2_UserFeedbackTest"
if errorlevel 1 goto FAILED

echo.
echo ============================================================
echo BUILD SUCCESSFUL
echo Output:
echo %CD%\RELEASE\HazzKaraokeHoster_v1.4.2_UserFeedbackTest\Hazz Karaoke Hoster.exe
echo ============================================================
explorer "%CD%\RELEASE\HazzKaraokeHoster_v1.4.2_UserFeedbackTest"
pause
exit /b 0

:FAILED
echo.
echo ============================================================
echo BUILD FAILED - see the error above.
echo ============================================================
pause
exit /b 1
