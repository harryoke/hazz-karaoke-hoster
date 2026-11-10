# Hazz Karaoke Hoster v2.30

v2.30 brings the FOLDERS work into the public release. It includes the offline Live Stems engine in the Windows x64 portable ZIP. AI Automix and beat analysis are not included.

## Changes since v2.20

| Feature | What changed |
| --- | --- |
| Local folder browser | Open **FOLDERS** or **Library > Browse Local Drives / Folders**. Browse drive trees, filter the current folder and drag tracks onto the music or karaoke deck. Music decks accept batches; karaoke loads the first compatible file. Browsing does not import files. |
| Video karaoke | Audio preparation and VLC video commands run in the background. The controls remain responsive while a slow decoder starts, and Hazz skips a redundant audio seek at the beginning. |
| Audio playback | The music and karaoke outputs request more buffer headroom. Karaoke waits for sustained clock drift before seeking, so a brief clock mismatch is less likely to interrupt audio. Windows can negotiate a different buffer. |
| Manual crossfade | In two-deck mode, start one deck, then the other. The second starts muted. Drag left for Deck 1, right for Deck 2, or in between to blend. Both tracks continue running. Moving the slider takes over from Auto Crossfade; tick **AUTO CROSSFADE** to return to automatic operation. |
| Live Stems | Open **STEMS** while a mono or stereo music track is playing. Mute or solo Vocals, Drums, Bass or Other on either deck. Solo selections add together; turning off the last Solo returns to your mute settings. **Restore original** clears the selection. |
| Complete Windows package | The LiveStems folder ships beside the executable. No second download, library upload or whole-track preparation is needed. |

## Install and try it

1. Close the previous copy of Hazz and extract the complete Windows x64 ZIP into a new folder.
2. Run **Hazz Karaoke Hoster.exe** and check that the header shows **v2.30**. Leave the bundled **LiveStems** and **Templates** folders beside it.
3. Use **FOLDERS** to load music onto the decks. Start both decks to try the crossfader. Open **STEMS** to warm the model, then try one deck before using both on a slower laptop.

Stem separation is an estimate and runs locally on the CPU. The first effect can take several seconds. If a block is not ready, including after a seek, Hazz plays the original audio until separation catches up. Test with your own music and output device before a show.

[Complete manual](COMPLETE_MANUAL.md) · [Quick start](DUMMYS_GUIDE.md) · [Live Stems details](LIVE_STEMS.md)

[Download the PDF manual update](Hazz_Karaoke_Hoster_v2.30_Manual_Update.pdf). Earlier PDF and Word guides are retained as historical references.
