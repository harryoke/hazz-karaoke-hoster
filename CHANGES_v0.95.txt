Hazz Karaoke Hoster v0.95

New since v0.90:
- Separate saved sound-device choices for Deck 1, Deck 2, quick-play and Karaoke.
- Direct SOUND DEVICES and NORMALIZE AUDIO controls on the top menu; flatter import and library menus.
- Normalize All Audio toggle with -24 to -12 dB RMS target slider (default -18), gradual levelling and per-player peak limiting. Original media files are unchanged. This is RMS levelling, not integrated LUFS normalization.
- Muted standby loading of the next automatically scheduled music track, with ready/problem status and decoder-opening checks.
- Pre-show queued-file access checks.
- Daily database/settings backups and 15-second recovery checkpoints for current music tracks and positions. Recovery does not start playback automatically.
- All v0.90 music-video overlay options remain included.

Validation: compilation, automated audio-processing and audience/interface regression checks, and ZIP integrity checks passed. Physical multi-device listening, hot-unplug and multi-hour playback testing remain outstanding. A successful cue check verifies opening, not every part of a media file. Per-player limiting does not guarantee the combined output of an external mixer cannot overload.