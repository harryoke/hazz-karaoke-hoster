using System.Text;

// Creates a small, standard RGB Photoshop document with two editable layers:
// a transparent bezel artwork layer and a visible safe-window guide layer.
const int width = 1920, height = 1080;
const int safeLeft = 96, safeTop = 54, safeRight = 1824, safeBottom = 1026;
var output = args.Length == 1 ? args[0] : Path.Combine("Documentation", "Templates", "Hazz_Audience_Bezel_Template_1920x1080.psd");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

var bezel = NewLayer("BEZEL ARTWORK - KEEP OUTSIDE WINDOW", PaintBezel());
var guide = NewLayer("VIDEO SAFE AREA - HIDE BEFORE EXPORT", PaintGuide());
using var file = File.Create(output);
using var writer = new BinaryWriter(file, Encoding.ASCII, leaveOpen: false);

WriteAscii(writer, "8BPS"); WriteU16(writer, 1); writer.Write(new byte[6]); WriteU16(writer, 4);
WriteU32(writer, (uint)height); WriteU32(writer, (uint)width); WriteU16(writer, 8); WriteU16(writer, 3);
WriteU32(writer, 0u); // colour mode data
WriteU32(writer, 0u); // image resources

using var section = new MemoryStream();
using (var layers = new BinaryWriter(section, Encoding.ASCII, leaveOpen: true))
{
    using var layerInfo = new MemoryStream();
    using (var lw = new BinaryWriter(layerInfo, Encoding.ASCII, leaveOpen: true))
    {
        WriteU16(lw, 2);
        WriteLayerRecord(lw, bezel);
        WriteLayerRecord(lw, guide);
        WriteLayerPixels(lw, bezel);
        WriteLayerPixels(lw, guide);
    }
    WriteU32(layers, checked((uint)layerInfo.Length));
    layerInfo.Position = 0; layerInfo.CopyTo(section);
    WriteU32(layers, 0u); // global mask info
}
WriteU32(writer, checked((uint)section.Length));
section.Position = 0; section.CopyTo(file);

WriteU16(writer, 0); // composite raw compression
foreach (var plane in bezel.Planes) writer.Write(plane);

Console.WriteLine($"Created {Path.GetFullPath(output)}");

static Layer NewLayer(string name, byte[][] planes) => new(name, planes);

static byte[][] PaintBezel()
{
    var result = NewPlanes();
    for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
    {
        var i = y * width + x;
        var inside = x >= safeLeft && x < safeRight && y >= safeTop && y < safeBottom;
        if (inside) continue;
        var edge = x < safeLeft || x >= safeRight || y < safeTop || y >= safeBottom;
        result[0][i] = edge ? (byte)22 : (byte)13;
        result[1][i] = edge ? (byte)50 : (byte)22;
        result[2][i] = edge ? (byte)82 : (byte)34;
        result[3][i] = 255;
    }
    return result;
}

static byte[][] PaintGuide()
{
    var result = NewPlanes();
    for (var y = safeTop; y < safeBottom; y++)
    for (var x = safeLeft; x < safeRight; x++)
    {
        var border = x < safeLeft + 5 || x >= safeRight - 5 || y < safeTop + 5 || y >= safeBottom - 5;
        if (!border) continue;
        var i = y * width + x;
        result[0][i] = 255; result[1][i] = 62; result[2][i] = 155; result[3][i] = 220;
    }
    return result;
}

static byte[][] NewPlanes() => Enumerable.Range(0, 4).Select(_ => new byte[width * height]).ToArray();

static void WriteLayerRecord(BinaryWriter writer, Layer layer)
{
    WriteU32(writer, 0u); WriteU32(writer, 0u); WriteU32(writer, (uint)height); WriteU32(writer, (uint)width);
    WriteU16(writer, 4);
    foreach (var channel in new short[] { 0, 1, 2, -1 }) { WriteI16(writer, channel); WriteU32(writer, checked((uint)(2 + width * height))); }
    WriteAscii(writer, "8BIM"); WriteAscii(writer, "norm"); writer.Write((byte)255); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
    using var extra = new MemoryStream();
    using (var ew = new BinaryWriter(extra, Encoding.ASCII, leaveOpen: true))
    {
        WriteU32(ew, 0u); WriteU32(ew, 0u);
        var name = Encoding.ASCII.GetBytes(layer.Name);
        ew.Write((byte)Math.Min(255, name.Length)); ew.Write(name, 0, Math.Min(255, name.Length));
        while (extra.Length % 4 != 0) ew.Write((byte)0);
    }
    WriteU32(writer, checked((uint)extra.Length)); extra.Position = 0; extra.CopyTo(writer.BaseStream);
}

static void WriteLayerPixels(BinaryWriter writer, Layer layer)
{
    foreach (var plane in layer.Planes) { WriteU16(writer, 0); writer.Write(plane); }
}

static void WriteAscii(BinaryWriter writer, string value) => writer.Write(Encoding.ASCII.GetBytes(value));
static void WriteU16(BinaryWriter writer, int value) { writer.Write((byte)(value >> 8)); writer.Write((byte)value); }
static void WriteI16(BinaryWriter writer, short value) => WriteU16(writer, unchecked((ushort)value));
static void WriteU32(BinaryWriter writer, uint value) { writer.Write((byte)(value >> 24)); writer.Write((byte)(value >> 16)); writer.Write((byte)(value >> 8)); writer.Write((byte)value); }

sealed record Layer(string Name, byte[][] Planes);
