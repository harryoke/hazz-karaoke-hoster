# Hazz Karaoke Hoster v1.71

Made For KJ/DJ's By A KJ/DJ

## Preview stability fix
Fixes the reproduced UCEERR_RENDERTHREADFAILURE crash associated with the live audience preview, including when switching skins. The preview now uses bounded snapshots instead of a live cross-window brush. Video preview remains muted. All v1.7 features are retained.

The same test that reproduced the failure now passes 60 live skin and size changes with image, GIF and video backgrounds. The affected user also confirmed Test 3 works. Normal hardware rendering remains enabled; the software-rendering workaround is not required for this fix.

## Update instructions
1. Close Hazz.
2. Extract the new ZIP into a new folder.
3. Run Hazz Karaoke Hoster.exe normally.
4. Check preview, skin switching and your usual audience output before the next show.

The standard portable folder and optional single EXE are both available. Existing settings and library data are retained. Preview text and images refresh at approximately seven frames per second; this does not reduce the actual audience display refresh rate. Video is mirrored through a separate muted player.

The v1.7 user manual and beginner guide still describe the controls in this maintenance release. Read these notes alongside those guides.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
