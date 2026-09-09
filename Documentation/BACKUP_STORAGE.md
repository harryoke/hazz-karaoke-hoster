# Backup storage — v0.99.1 test

Automatic database backups now use compressed ZIP archives rather than full-size daily .db files. At most three generated ZIPs are retained, within a 1 GB total budget. The newest archive is always retained even if it alone exceeds that limit. Compression savings depend on your database; no fixed percentage is guaranteed.

The archive contains hazz-hoster.db and top-level settings JSON files. Compression and validation run in the background. A temporary uncompressed database snapshot is required while creating the ZIP; free space must accommodate it and the new archive. A failed backup is reported in the host console and does not retire older backups.

Old uncompressed .db backups and matching .db.settings folders are left intact. After checking the new ZIP, you can remove unwanted old backup pairs from Library > Open Automatic Backups. These are backup copies; do not delete the live hazz-hoster.db from the main database folder. Manual backups are not included in automatic ZIP retention.

To inspect a backup, extract the ZIP to a separate folder. There is no full-database restore wizard. Venue singer snapshots in venue-singers must still be backed up separately with venue-profiles.json; the automatic database archive does not include that subfolder.
