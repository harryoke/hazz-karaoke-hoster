using System.IO;

namespace HazzKaraokeHoster.App;

// Change frame delays in an in-memory copy, leaving pixels, disposal/transparency
// metadata and the user's original file intact. GIF delays use hundredths of a second.
internal static class GifPlaybackTiming
{
    public static void ScaleDelays(byte[] data, double speed)
    {
        if (!double.IsFinite(speed) || speed < 0.25 || speed > 4) throw new ArgumentOutOfRangeException(nameof(speed));
        void Require(int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > data.Length - count) throw new InvalidDataException("Truncated GIF.");
        }
        Require(0, 13);
        var header = System.Text.Encoding.ASCII.GetString(data, 0, 6);
        if (header != "GIF87a" && header != "GIF89a") throw new InvalidDataException("Invalid GIF header.");
        var pos = 13;
        if ((data[10] & 128) != 0) pos += 3 * (1 << ((data[10] & 7) + 1));
        void SkipBlocks()
        {
            while (true)
            {
                Require(pos, 1);
                int size = data[pos++];
                if (size == 0) return;
                Require(pos, size);
                pos += size;
            }
        }
        while (true)
        {
            Require(pos, 1);
            switch (data[pos++])
            {
                case 0x3B: return;
                case 0x21:
                    Require(pos, 1);
                    int label = data[pos++];
                    if (label == 0xF9)
                    {
                        Require(pos, 6);
                        if (data[pos] != 4 || data[pos + 5] != 0) throw new InvalidDataException("Invalid GIF control block.");
                        int delay = data[pos + 2] | data[pos + 3] << 8;
                        // Match the decoder's 100ms fallback for unspecified/zero delays.
                        int scaled = Math.Clamp((int)Math.Round((delay == 0 ? 10 : delay) / speed), 1, ushort.MaxValue);
                        data[pos + 2] = (byte)scaled;
                        data[pos + 3] = (byte)(scaled >> 8);
                        pos += 6;
                    }
                    else SkipBlocks();
                    break;
                case 0x2C:
                    Require(pos, 9);
                    int packed = data[pos + 8];
                    pos += 9;
                    if ((packed & 128) != 0) pos += 3 * (1 << ((packed & 7) + 1));
                    Require(pos, 1);
                    pos++; // LZW code size
                    SkipBlocks();
                    break;
                default: throw new InvalidDataException("Invalid GIF block.");
            }
        }
    }
}
