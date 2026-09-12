using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HazzKaraokeHoster.Playback.Cdg;

static byte[] Packet(byte instruction, params byte[] data)
{
    var p = new byte[24]; p[0] = 9; p[1] = instruction;
    data.CopyTo(p, 4); return p;
}
static byte[] Frame(byte[] bytes, double seconds)
{
    var decoder = new CdgDecoder(); decoder.Load(bytes); decoder.Seek(TimeSpan.FromSeconds(seconds));
    var frame = new byte[CdgDecoder.Width * CdgDecoder.Height * 4]; decoder.CopyBgra32(frame); return frame;
}
var palette = new byte[16]; palette[0] = 59; palette[1] = 46; // palette zero = #EEEEEE
var bytes = Packet(30, palette).Concat(Packet(28, new byte[16])).ToArray();
var result = Frame(bytes, 1);
if (result[0] != 238 || result[1] != 238 || result[2] != 238 || result[3] != 255)
    throw new Exception("All-zero transparency must preserve opaque pale grey.");
var alpha = new byte[16]; alpha[0] = 32;
var decoder = new CdgDecoder(); decoder.Load(bytes.Concat(Packet(28, alpha)).ToArray());
decoder.Seek(TimeSpan.FromSeconds(1)); decoder.CopyBgra32(result);
if (result[3] != 127) throw new Exception("Per-colour transparency was not decoded.");
decoder.Seek(TimeSpan.Zero); decoder.CopyBgra32(result);
if (result[3] != 255) throw new Exception("Backward seek must reset alpha.");
Console.WriteLine("PASS opaque grey, per-colour alpha and backward-seek reset");
if (args.Length == 2)
{
    var frame = Frame(File.ReadAllBytes(args[0]), 11);
    var image = BitmapSource.Create(CdgDecoder.Width,CdgDecoder.Height,96,96,PixelFormats.Bgra32,null,frame,CdgDecoder.Width*4);
    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
    using var output = File.Create(args[1]); png.Save(output);
    Console.WriteLine("Rendered actual song at 11 seconds.");
}
