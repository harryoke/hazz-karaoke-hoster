namespace HazzKaraokeHoster.App;

public sealed class SoundFxPad
{
    public string Label { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Colour { get; set; } = "#276180";
    public double Volume { get; set; } = 0.8;
    public bool DuckMusic { get; set; } = true;
}
public sealed class SoundFxSettings
{
    public List<SoundFxPad> Pads { get; set; } = new();
    public int KamikazeSlot { get; set; } = -1;
    public string? OutputDeviceId { get; set; }
    public void Normalize()
    {
        Pads ??= new();
        Pads = Pads.Take(9).Select((pad,index) => {
            pad ??= new();
            pad.Label = string.IsNullOrWhiteSpace(pad.Label) ? $"FX {index+1}" : pad.Label.Trim()[..Math.Min(pad.Label.Trim().Length,40)];
            pad.FilePath ??= "";
            pad.Colour ??= "#276180";
            pad.Volume = double.IsFinite(pad.Volume) ? Math.Clamp(pad.Volume,0,1) : 0.8;
            return pad;
        }).ToList();
        while(Pads.Count<9) Pads.Add(new() { Label=$"FX {Pads.Count+1}" });
        if(KamikazeSlot is < -1 or > 8) KamikazeSlot=-1;
    }
}
