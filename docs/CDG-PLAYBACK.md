# CD+G playback implementation notes

Hazz v0.2 contains its own basic CD+G graphics decoder rather than relying on a video codec for `.cdg` files.

The decoder handles the standard instruction set used by ordinary karaoke CD+G files:
- Memory Preset
- Border Preset
- Tile Block Normal
- Tile Block XOR
- Scroll Preset
- Scroll Copy
- Define Transparent Color
- Load Color Table Low
- Load Color Table High

The graphics raster is 300 x 216 pixels and packets are advanced from the audio position at 300 packets per second.

## Sync control
`CdgTimingController` stores a timeline shift in 0.25 second steps.
- `-0.25s`: display the graphics 0.25 seconds earlier than their original timing.
- `+0.25s`: display the graphics 0.25 seconds later than their original timing.

The renderer converts this into the CD+G stream time on every update, so the control can be changed while a song is playing.

## ZIP files
Only the active CDG/audio pair is copied to `%LOCALAPPDATA%\Hazz Karaoke Hoster\Temp\<session>`; the original ZIP is never modified. The temporary directory is deleted when the track is replaced or the application closes, where Windows file locking permits.

## First Windows validation
Test a range of manufacturers, especially files using scroll commands, XOR tiles, colour cycling and splash graphics. Any rendering discrepancy should be tested using the original `.cdg` so the decoder can be corrected at instruction level.
