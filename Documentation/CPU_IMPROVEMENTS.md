# CPU improvements — first pass

- Next-track readiness checks now run only on their dedicated one-second timer, instead of also running on every fast crossfade timer tick.
- When the host window is minimized, routine timeline, deck LED and button-light refreshes are skipped. Music automation, crossfades, audience-video synchronization, cue checks and recovery timers continue.
- Hidden deck LED panels no longer build text strings or update scrolling transforms.

Audio decoding, buffer sizes, normalization processing and fade timing are unchanged. These changes remove identifiable repeated work; whole-app CPU savings have not been measured and will depend on how the app is used.
