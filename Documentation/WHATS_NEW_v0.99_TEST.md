# v0.99 test — Shutdown correction

Fixes the misleading Venue Save Failed message: "Cannot set Visibility to Visible or call Show, ShowDialog, Close, or WindowInteropHelper.EnsureHandle while a Window is closing."

Hazz now waits for the original close request to finish before saving and completing shutdown. This covers saves that finish immediately, including when no venue is active. Actual save failures still leave the app open for retry.

Venue saves remain on request, before changing venue singer lists, and on normal shutdown. There is no periodic venue-save timer. Without a venue, singer history continues saving in the main database.
