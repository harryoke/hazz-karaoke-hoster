@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo        HAZZ KARAOKE HOSTER - BUILD RUNNABLE EXE
echo ============================================================
echo.

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
  -o "RELEASE\HazzKaraokeHoster"

if errorlevel 1 goto FAILED

echo.
echo ============================================================
echo BUILD SUCCESSFUL
echo.
echo EXE:
echo %CD%\RELEASE\HazzKaraokeHoster\Hazz Karaoke Hoster.exe
echo ============================================================
echo.
explorer "%CD%\RELEASE\HazzKaraokeHoster"
pause
exit /b 0

:FAILED
echo.
echo ============================================================
echo BUILD FAILED - see the errors above.
echo ============================================================
pause
exit /b 1
