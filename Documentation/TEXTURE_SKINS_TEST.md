# Metal and marble skins — v1.9 test build

Made For KJ/DJ's By A KJ/DJ

Open **DISPLAY → Console skin / layout**, then choose:

- Azure brushed steel
- Ruby brushed steel
- Amethyst brushed steel — light
- Bronze brushed steel
- Gold brushed steel
- Silver brushed steel — light
- Graphite brushed steel
- Rose marble — light
- Onyx marble

These nine choices use the supplied metal and marble artwork on the console casing and player panels. Lists and input fields remain opaque for readability. Playback buttons retain their functional colours. Light skins use dark text; the dark skins use light text.

The new textures use the familiar Classic arrangement with the same live controls and event handlers. Classic, Midnight, Copper and Daylight remain selectable. Returning to Classic restores the original brushes. All normal, single-deck, Karaoke and Focus modes remain available.

The selected skin is saved through the existing console-only preference writer. Changing it does not save over audience-display settings. Bitmaps are decoded at a bounded size, cached and frozen; there is no animated texture or new rendering timer.

To test, extract the complete Windows ZIP into a new folder, close your other copy of Hazz, and start Hazz Karaoke Hoster.exe. The build is labelled **v1.9 SKINS TEST**. It uses your normal library and settings. Source users can build HazzKaraokeHoster.sln in VS2026 using Release and the .NET 10 desktop workload.

Screenshots use illustrative sample rows and the actual WPF console view. Layout checks cover all skins at 1920×1080, 1366×768 and 1024×768, normal/large text and all four console modes. A short switching test also exercises the audience preview with background image/GIF/video assets. This does not replace rehearsal on the show laptop and external TV.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
