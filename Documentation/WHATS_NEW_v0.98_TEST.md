# v0.98 test — Automatic venue singer saving

Save or load a venue singer list once to activate it. Hazz keeps that venue's singers, history and queue updated when explicitly saved, before changing singer lists and when closing normally. The active venue is remembered after restart. No venue is required: the main singer database always retains names and permanent history during normal use.

Starting blank or restoring a backup disconnects the named venue, preventing accidental overwrites. Applying display/audio settings alone does not switch singer lists. See [Venue Profiles](VENUE_PROFILES.md).

Validation: application build, singer database checks and isolated automatic-save tests covering queue/history capture, concurrent saves, revision cleanup, restart selection, disconnected operation and missing-profile failure. Real show-equipment testing remains pending. This is a local test release, not a published update.

