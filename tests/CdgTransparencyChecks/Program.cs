using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HazzKaraokeHoster.Playback.Cdg;

var timing = new HazzKaraokeHoster.Playback.CdgTimingController();
timing.Set(1);
if (timing.GetGraphicsTime(TimeSpan.FromSeconds(10)).TotalSeconds != 11) throw new Exception("Positive CDG sync must advance graphics");
timing.Set(-1);
if (timing.GetGraphicsTime(TimeSpan.FromSeconds(10)).TotalSeconds != 9) throw new Exception("Negative CDG sync must delay graphics");
timing.Reset();
if (timing.GetGraphicsTime(TimeSpan.FromSeconds(10)).TotalSeconds != 10) throw new Exception("CDG sync reset");
Console.WriteLine("PASS: CDG sync positive advances, negative delays, reset unchanged.");
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
var custom = new CdgDecoder();
var tile = new byte[16]; tile[0]=3; tile[1]=7; tile[2]=2; tile[3]=2; tile[4]=32;
custom.Load(Packet(1,3,0).Concat(Packet(6,tile)).ToArray());custom.Seek(TimeSpan.FromSeconds(1));
custom.CopyBgra32(result,-1);
if(custom.BackgroundColour!=3 || result[3]!=0 || result[(24*CdgDecoder.Width+12)*4+3]!=255) throw new Exception("Auto key must remove preset background and preserve contrasting tile pixels");
custom.CopyBgra32(result,7);
if(result[3]!=255 || result[(24*CdgDecoder.Width+12)*4+3]!=0) throw new Exception("Manual palette key");
custom.CopyBgra32(result);
if(result[3]!=255) throw new Exception("Disable key must restore original alpha");
custom.Seek(TimeSpan.Zero);
if(custom.BackgroundColour!=0) throw new Exception("Seek must reset automatic background colour");
Console.WriteLine("PASS: automatic/manual colour key, original alpha restoration and seek reset.");
