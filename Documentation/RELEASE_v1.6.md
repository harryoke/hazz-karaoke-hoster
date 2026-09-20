# Hazz Karaoke Hoster v1.6 audience display guide

Version 1.6 adds Fit or Stretch for karaoke playback, separate rotation and message switches, and a second independent scrolling message. The existing Fit display and combined primary scroller remain the defaults. The second scroller starts switched off.

## Fill the audience screen with karaoke
1. Open DISPLAY, then Audience Settings — Backgrounds, Logo, Scroller.
2. Find Karaoke playback sizing beside the scroller switches.
3. Choose Fit to show the entire picture in its original proportions. A square or 4:3 song on a widescreen TV will have side borders.
4. Choose Stretch to fill the audience display. This changes the picture proportions, so faces and lettering may look wider or taller.
5. Play a CD+G or karaoke video and check the TV. The setting applies to both Windows and VLC karaoke video engines, including Windows fallback. It can also be changed during playback.

This setting affects the audience karaoke picture, not the private host preview, ordinary music videos or idle artwork. The existing Screen fit control under Singer-view background still controls images, GIFs and background videos. Black borders recorded inside a video remain part of that video; Stretch cannot remove them independently. Crisp or smooth CD+G remains available.

## Show a message without revealing the singer order
1. Open DISPLAY > Audience Settings.
2. Leave Primary scroller switched on.
3. Switch off Show singer rotation.
4. Leave Show custom message switched on and type your announcement into the message box beside Primary scroller.
5. If you also want to hide the separate upcoming-singers panel, switch off Show next 4 singers at the top of this panel.

Only the announcement now scrolls. You can rearrange the singers privately. To show the order again, enable Show singer rotation. Both content switches can be on together; rotation comes first and the message follows. Turning Primary scroller off hides the entire first bar. Turning both content switches off also hides it. An empty message adds no text. When rotation is enabled but there are no singers, the bar says Singer rotation empty.

## Add a second scrolling message
1. Switch on Second message scroller.
2. Enter the second message in its box, for example DRINKS OFFERS — ASK AT THE BAR. A blank message keeps this bar hidden.
3. Choose Second font. Drag its Size slider to choose 16–120 pixels; the number beside it shows the selected size.
4. Drag the second Speed slider to choose how fast that message moves. This does not change the primary scroller speed.
5. Press SECOND COLOUR to choose this message's colour.
6. Use the second Move away from edge slider if the TV cuts off the edge of the message.

The second message works even when Primary scroller is off. Each bar has its own font, size, speed and inset. Primary rotation and message colours still use the ROTATION and VENUE MSG buttons. Existing primary text outline settings continue to apply.

## Keep the two bars apart
1. Use Position in the primary scroller controls to choose Top or Bottom.
2. The second bar automatically uses the opposite edge. Primary Bottom means second Top; primary Top means second Bottom. This also determines the second bar's edge when the primary is off.
3. Try the edge-inset sliders while looking at the audience TV. A larger number moves that bar inward.

Hazz limits the effective inset so each bar stays in its own half of the audience screen. The requested slider value is retained, but may be reduced on a short display or with large fonts to keep the bars apart. Extremely short windows can clip large text; enlarge the audience window or reduce its font size. The upcoming-singers panel keeps clearance from visible bars.

## What happens during songs and when saving
Both scrollers and the upcoming-singers panel hide during karaoke playback and the Kamikaze announcement, as the original scroller did. They return afterwards. To show enabled scrollers over ordinary music videos, tick Both scrollers under SHOW DURING MUSIC VIDEOS. Leave it off for an unobstructed music video. Permanent logo behaviour is unchanged.

Settings apply live. Press CLOSE to dismiss the panel. Close Hazz normally to retain your display choices; do not end it using Task Manager. When using venue profiles, save or update that profile to keep these choices with the venue, following the existing venue save rules. An older profile without the new fields uses Fit, enables both primary content switches, and leaves the second bar off. Existing singers, favourites and playlists are not changed by these options.

## Update or build v1.6
Download the normal Windows ZIP and extract the entire folder, or choose the optional Single EXE ZIP. Close the old Hazz before launching the new copy. Both use your existing Hazz data location. Keep all runtime folders with the normal download. The single EXE extracts its bundled runtime automatically. No separate VLC installation is needed.

To build yourself, install Visual Studio 2026 with .NET desktop development and .NET 10, open HazzKaraokeHoster.sln, select Release / x64 and build. The normal app is in RELEASE/HazzKaraokeHoster and the optional single executable is in RELEASE/SingleFile.

Copyright © 2026 Hazz Karaoke. All rights reserved in the original Hazz Karaoke Hoster code, documentation, branding and artwork. Third-party components remain subject to their respective licences.
