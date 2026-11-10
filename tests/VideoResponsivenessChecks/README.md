# Video responsiveness regression checks

The bundled fixtures are generated test media: a 90-second 160x90 MP4 (MPEG-4 Part 2) and four-second quiet stereo PCM tone. They contain no user media. Run from the source root:

```
dotnet run --project tests/VideoResponsivenessChecks -c Release -- tests/VideoResponsivenessChecks/Fixtures/video.mp4 tests/VideoResponsivenessChecks/Fixtures/audio.wav
```

Requires Windows with an active default WASAPI render endpoint. The checks open a small video window and briefly play the quiet tone. A ten-second stall is deliberately injected into the actual production decoder queue. Dispatcher progress and synchronous control latency are checked during that stall, followed by real decoding, pause, seek, tempo, rapid source replacement, restart and cleanup. Audio tests exercise the production background preparation API and its cancellation/error behavior. They do not open the live host or user database.

For broader audience regression checks, build and run tests/LibVlcSmoke with the video fixture. Repeat with --windows-video for the Windows backend. These cover overlays, mirrored preview, A/V offsets, backend preferences, fallback and repeated window closure.
