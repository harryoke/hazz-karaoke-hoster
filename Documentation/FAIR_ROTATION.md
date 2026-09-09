# Automatic fair rotation

Enable **Show > Automatic Fair Singer Rotation**.

Open **Show > Rotation Settings** to choose a main rule and tie-breaker independently: Fewest turns, Longest waiting or Arrival order. Optional consecutive-turn protection puts the most recent performer behind other ready singers, before applying the selected rules. It does not prevent the only ready singer from performing. Settings survive restart even with an empty queue and are included in venue profiles.

Ready singers with fewer turns in this show go first. Equal turn counts use arrival order for newcomers, and the oldest previous turn for returning singers. Held singers and singers with no queued song are placed behind ready singers. The checked-out performer remains in their current queue slot while other singers are reordered.

A turn is counted once when Play checks out the singer's song, matching Hazz's existing rotation handoff. Pause/resume does not add a turn. A stopped or failed performance still counts as a started turn. Imported historical performances are not counted. Late arrivals can receive consecutive turns to catch up under this fewest-turns policy.

Manual moves are replaced while fair mode is enabled. Disable the option to make a host override. New Show resets turn records; recovery snapshots preserve them. Singers are recognised by database ID when available, otherwise by their trimmed name ignoring case. Existing sessions started before this feature have no retrospective turn count.
