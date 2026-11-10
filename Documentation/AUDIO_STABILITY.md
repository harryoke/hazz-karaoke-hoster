> Included in v2.30. See the [v2.30 release notes](RELEASE_v2.30.md) for current installation instructions.

# v2.20.3 FOLDERS

Music and karaoke request 240 ms of output buffering (previously 120 ms). Windows may negotiate a different WASAPI buffer. Karaoke now requires three consecutive large clock discrepancies in the same direction before automatically seeking. Manual seeks are unchanged.

Verified with the drift-filter checks, routed WASAPI karaoke audio, and the video responsiveness suite. The reported intermittent dropout was not reproduced in this test. Extract the Windows build into a new folder to try it.
