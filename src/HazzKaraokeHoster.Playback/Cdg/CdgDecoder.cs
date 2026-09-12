namespace HazzKaraokeHoster.Playback.Cdg;

/// <summary>
/// Decodes the graphics channel used by CD+G karaoke discs.
/// CD+G packets arrive at 300 packets/second. The decoder keeps the 300x216
/// indexed screen and 16-colour palette in memory and can seek by rebuilding
/// from the start when the requested graphics time moves backwards.
/// </summary>
public sealed class CdgDecoder
{
    public const int Width = 300;
    public const int Height = 216;
    public const int PacketsPerSecond = 300;
    public const int PacketSize = 24;

    private const byte CdgCommand = 0x09;
    private const byte InstructionMask = 0x3F;

    private readonly byte[] _pixels = new byte[Width * Height];
    private readonly uint[] _palette = new uint[16];
    private byte[] _packets = Array.Empty<byte>();
    private int _decodedPacketCount;
    private readonly byte[] _alpha = new byte[16];
    private int _horizontalOffset;
    private int _verticalOffset;

    public long FrameVersion { get; private set; }
    public int TotalPackets => _packets.Length / PacketSize;
    public TimeSpan Duration => TimeSpan.FromSeconds(TotalPackets / (double)PacketsPerSecond);

    public void Load(byte[] cdgBytes)
    {
        ArgumentNullException.ThrowIfNull(cdgBytes);
        _packets = cdgBytes;
        Reset();
    }

    public void Reset()
    {
        Array.Clear(_pixels);
        Array.Fill(_palette, 0xFF000000u);
        _decodedPacketCount = 0;
        Array.Fill(_alpha, (byte)255);
        _horizontalOffset = 0;
        _verticalOffset = 0;
        FrameVersion++;
    }

    /// <summary>
    /// Moves the decoder to the requested graphics time. Moving backwards
    /// intentionally rebuilds from packet zero; this is deterministic and
    /// keeps seeking/correction logic simple and safe.
    /// </summary>
    public void Seek(TimeSpan graphicsTime)
    {
        var seconds = Math.Max(0, graphicsTime.TotalSeconds);
        var targetPacket = Math.Clamp((int)Math.Floor(seconds * PacketsPerSecond), 0, TotalPackets);

        if (targetPacket < _decodedPacketCount)
            Reset();

        while (_decodedPacketCount < targetPacket)
        {
            DecodePacket(_decodedPacketCount);
            _decodedPacketCount++;
        }
    }

    /// <summary>Copies the current 300x216 frame into a BGRA32 buffer.</summary>
    public void CopyBgra32(byte[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var required = Width * Height * 4;
        if (destination.Length < required)
            throw new ArgumentException($"Destination buffer must contain at least {required} bytes.", nameof(destination));

        var o = 0;
        for (var y = 0; y < Height; y++)
        {
            // Fine-scroll offsets are represented as a wraparound viewport.
            var sourceY = (y + _verticalOffset) % Height;
            for (var x = 0; x < Width; x++)
            {
                var sourceX = (x + _horizontalOffset) % Width;
                var index = _pixels[sourceY * Width + sourceX] & 0x0F;
                var argb = _palette[index];
                var alpha = _alpha[index];
                destination[o++] = (byte)argb;          // B
                destination[o++] = (byte)(argb >> 8);   // G
                destination[o++] = (byte)(argb >> 16);  // R
                destination[o++] = alpha;               // A
            }
        }
    }

    private void DecodePacket(int packetIndex)
    {
        var start = packetIndex * PacketSize;
        if (start + PacketSize > _packets.Length) return;

        var command = (byte)(_packets[start] & 0x3F);
        if (command != CdgCommand) return;

        var instruction = (byte)(_packets[start + 1] & InstructionMask);
        var data = _packets.AsSpan(start + 4, 16);

        switch (instruction)
        {
            case 1:  MemoryPreset(data); break;
            case 2:  BorderPreset(data); break;
            case 6:  TileBlock(data, xor: false); break;
            case 20: Scroll(data, copy: false); break;
            case 24: Scroll(data, copy: true); break;
            case 28: DefineTransparentColor(data); break;
            case 30: LoadColorTable(data, 0); break;
            case 31: LoadColorTable(data, 8); break;
            case 38: TileBlock(data, xor: true); break;
            default: return;
        }

        FrameVersion++;
    }

    private void MemoryPreset(ReadOnlySpan<byte> data)
    {
        var color = (byte)(data[0] & 0x0F);
        var repeat = data[1] & 0x0F;
        // The same memory-preset command is commonly repeated for reliability.
        // Only the first repeat needs to clear the screen.
        if (repeat == 0) Array.Fill(_pixels, color);
    }

    private void BorderPreset(ReadOnlySpan<byte> data)
    {
        var color = (byte)(data[0] & 0x0F);
        for (var y = 0; y < Height; y++)
        {
            var row = y * Width;
            if (y < 12 || y >= Height - 12)
            {
                Array.Fill(_pixels, color, row, Width);
                continue;
            }

            Array.Fill(_pixels, color, row, 6);
            Array.Fill(_pixels, color, row + Width - 6, 6);
        }
    }

    private void TileBlock(ReadOnlySpan<byte> data, bool xor)
    {
        var color0 = (byte)(data[0] & 0x0F);
        var color1 = (byte)(data[1] & 0x0F);
        var row = (data[2] & 0x1F) * 12;
        var column = (data[3] & 0x3F) * 6;

        if (row >= Height || column >= Width) return;

        for (var y = 0; y < 12 && row + y < Height; y++)
        {
            var bits = data[4 + y] & 0x3F;
            for (var x = 0; x < 6 && column + x < Width; x++)
            {
                var selected = (bits & (1 << (5 - x))) != 0 ? color1 : color0;
                var index = (row + y) * Width + column + x;
                _pixels[index] = xor ? (byte)((_pixels[index] ^ selected) & 0x0F) : selected;
            }
        }
    }

    private void LoadColorTable(ReadOnlySpan<byte> data, int firstIndex)
    {
        for (var i = 0; i < 8; i++)
        {
            var value = ((data[i * 2] & 0x3F) << 6) | (data[i * 2 + 1] & 0x3F);
            var r = (value >> 8) & 0x0F;
            var g = (value >> 4) & 0x0F;
            var b = value & 0x0F;
            _palette[firstIndex + i] = 0xFF000000u
                | ((uint)(r * 17) << 16)
                | ((uint)(g * 17) << 8)
                | (uint)(b * 17);
        }
    }

    private void DefineTransparentColor(ReadOnlySpan<byte> data)
    {
        // Instruction 28 contains one transparency value for each palette
        // entry, not a single transparent colour index. An all-zero packet
        // means every colour is opaque, including palette entry zero.
        for (var i = 0; i < 16; i++)
            _alpha[i] = (byte)(255 - ((data[i] & 0x3F) << 2));
    }

    private void Scroll(ReadOnlySpan<byte> data, bool copy)
    {
        var fillColor = (byte)(data[0] & 0x0F);
        var horizontalCommand = (data[1] & 0x30) >> 4;
        var verticalCommand = (data[2] & 0x30) >> 4;
        _horizontalOffset = data[1] & 0x07;
        _verticalOffset = data[2] & 0x0F;

        if (horizontalCommand == 1) ShiftHorizontal(6, fillColor, copy);      // right
        else if (horizontalCommand == 2) ShiftHorizontal(-6, fillColor, copy); // left

        if (verticalCommand == 1) ShiftVertical(12, fillColor, copy);       // down
        else if (verticalCommand == 2) ShiftVertical(-12, fillColor, copy);   // up
    }

    private void ShiftHorizontal(int amount, byte fillColor, bool copy)
    {
        var source = (byte[])_pixels.Clone();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var fromX = x - amount;
                if (fromX >= 0 && fromX < Width)
                    _pixels[y * Width + x] = source[y * Width + fromX];
                else if (copy)
                {
                    fromX = (fromX % Width + Width) % Width;
                    _pixels[y * Width + x] = source[y * Width + fromX];
                }
                else
                    _pixels[y * Width + x] = fillColor;
            }
        }
    }

    private void ShiftVertical(int amount, byte fillColor, bool copy)
    {
        var source = (byte[])_pixels.Clone();
        for (var y = 0; y < Height; y++)
        {
            var fromY = y - amount;
            for (var x = 0; x < Width; x++)
            {
                if (fromY >= 0 && fromY < Height)
                    _pixels[y * Width + x] = source[fromY * Width + x];
                else if (copy)
                {
                    var wrappedY = (fromY % Height + Height) % Height;
                    _pixels[y * Width + x] = source[wrappedY * Width + x];
                }
                else
                    _pixels[y * Width + x] = fillColor;
            }
        }
    }
}
