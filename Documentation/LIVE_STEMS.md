# Hazz v2.30 - Live Stems preview

1. Extract the complete Windows x64 ZIP. The LiveStems engine is already included.
2. Open the extracted v2.30 folder and run Hazz Karaoke Hoster.exe.
3. Launch Hazz, start a regular music track and open STEMS. Choose Mute or Solo under Vocals, Drums, Bass or Other on either deck. Solo buttons add or remove individual parts, so Vocals and Drums can play together. When the last Solo is turned off, the previous mute settings apply again. Restore original resets that deck. The deck faders, crossfader, tempo and routing still operate after stem mixing.

The model warms for a playing deck while the panel is open. No whole-track preparation is required. The first effect starts rolling inference around the current playhead. Finished blocks are mixed in memory; the audio callback never waits for inference. If a block is missing, the original mix continues, so a muted part may briefly return while catching up or after a seek. Status shows when separated audio is active. Returning to the original mix suspends new inference; stopping or replacing the track cancels its worker. The optional engine and model are local and CPU-only. No source audio is edited or uploaded. AI Automix and beat-analysis code are not included.

The preview supports mono/stereo audio files on the two music decks. Separation is estimated; bleed is possible. Cold request to first usable block was about 11 seconds in the paced check on this PC. Warm 10-second inference took about 1.2 seconds on the same PC. Laptop speed and two-deck throughput may differ. Try one deck first on the laptop that has had dropouts.

Checks: real model response, playback-paced block transition, forward/backward seeking, cancellation, mute/solo masks, original fallback and restoration. Existing manual crossfader and five host layouts passed. Packaged VLC/SQLite runtime check passed. No physical listening test or live laptop soak was performed.

Source check: build tests/VideoResponsivenessChecks, then run the DLL with --live-stems followed by a local mono/stereo WAV at least 10 seconds long and the path to the installed LiveStems engine folder.
