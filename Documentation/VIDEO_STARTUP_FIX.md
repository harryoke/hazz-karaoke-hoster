> Included in v2.30. See the [v2.30 release notes](RELEASE_v2.30.md) for current installation instructions.

# v2.20.2 FOLDERS - video startup fix

3 November 2026. Continues the original v2.20-based FOLDERS branch; no AI Automix/stems code or dependencies were imported.

## Changes

- Karaoke Media Foundation audio preparation runs on a background task in an independent pipeline. The UI adopts it when preparation succeeds; concurrent load/play requests are guarded, failed preparation preserves the previous deck, and cancellation disposes the unused pipeline. Old audio pipeline cleanup runs in the background.
- Native audience/preview video initialization, start, pause, seek, rate changes, clock reads, stop and disposal use an ordered background queue. The UI reads cached clocks. WPF HWND attachment/detachment remains on its dispatcher, ordered outside native start/stop operations. Late source callbacks are discarded; cleanup waits behind outstanding native work without blocking the dispatcher.
- A redundant audio seek to 00:00 is skipped on a freshly prepared source. Startup pause/resume now honors the latest requested playback state.
- Existing folder browser and original music/queue behavior retained. No media files are edited.

## Validation

- Production build/publish: zero compiler warnings/errors. Bundled VLC and SQLite package check passed. Self-contained Windows x64; no separate .NET installation needed.
- 17 video/audio responsiveness checks passed using generated MP4 and PCM WAV fixtures. Includes production background Media Foundation preparation on a selected WASAPI output, duration/play/seek/stop, failed preparation, cancellation, real native playback, tempo, pause, restart, rapid replacement and orderly cleanup.
- A deliberate ten-second stall in the production decoder queue left the dispatcher responsive: 335 timer ticks, maximum recorded gap 81 ms. Load/play/pause calls returned within the 500 ms test bound. This is a controlled regression test, not a reproduction with the user's particular file.
- Existing audience smoke suite passed with native VLC and a generated MJPEG AVI: fit/stretch, A/V offsets, pause/seek, preview, engine switching, music-video handoff, repeated disposal and failed-source fallback. Windows-backend smoke suite passed with generated MPEG-4 Part 2 MP4: playback, seek, pause, A/V offsets, preview, handoff and repeated disposal.
- All 23 local-folder feature regression checks passed.

## Scope and limitations

The user's affected video was not supplied, so its codec/drive-specific ten-second delay has not been reproduced. Slow codecs or storage can still take time to prepare; this change keeps the managed preparation/native decoder work off the UI thread. No live-show soak, physical drag gesture or exhaustive device/codec matrix was performed.

The first generated AVI fixture failed a Windows MediaElement seek check; the generated MP4 passed the same Windows suite. Native VLC passed both fixtures. Audio playback/preparation checks use the active default endpoint explicitly through WASAPI. The environment's legacy WaveOut driver threw an SEH exception on close in both the new background-preparation experiment and a synchronous baseline experiment; that driver path is not claimed as passing. Native video teardown also emits Windows thumbnail clipping diagnostics in this test environment; playback and cleanup assertions passed.

## Install and test

Extract the Windows ZIP into a new folder and run Hazz Karaoke Hoster.exe. Keep the previous build for comparison. Try the same video that froze, including the first Play after launching. No AI setup is involved.

Source: BUILD-FOLDERS.cmd runs folder and responsiveness checks before publishing. The latter requires an active Windows WASAPI endpoint and briefly opens a fixture window/plays a quiet tone. See tests/VideoResponsivenessChecks/README.md. Generated fixtures and detailed logs are included in the source package.
