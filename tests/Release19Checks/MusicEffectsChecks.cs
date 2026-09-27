using NAudio.Wave;
using HazzKaraokeHoster.Playback;
static class MusicEffectsChecks
{
    static void Check(bool pass,string label) { if(!pass)throw new Exception(label); Console.WriteLine("PASS "+label); }
    sealed class Signal(int rate,int channels):ISampleProvider
    {
        public WaveFormat WaveFormat { get; }=WaveFormat.CreateIeeeFloatWaveFormat(rate,channels);
        public bool Silent; public bool LeftOnly; public double Amplitude=.7; long frame;
        public int Read(float[] b,int offset,int count)
        {
            for(int i=0;i<count;i+=channels,frame++)for(int c=0;c<channels;c++)b[offset+i+c]=Silent || (LeftOnly && c>0)?0:(float)(Amplitude*Math.Sin(frame*2*Math.PI*431/rate));
            return count;
        }
    }
    public static void Run()
    {
        foreach(var rate in new[]{44100,48000}) foreach(var channels in new[]{1,2}) foreach(var mode in MusicEffectsState.Modes)
        {
            var state=new MusicEffectsState { Mode=mode,MixPercent=40 }; var input=new Signal(rate,channels); var provider=new MusicEffectsSampleProvider(input,()=>state.Options);
            var reference=new Signal(rate,channels); var buffer=new float[480*channels+6]; var dry=new float[480*channels+6];
            Array.Fill(buffer,42); provider.Read(buffer,3,480*channels); reference.Read(dry,3,480*channels);
            Check(buffer.Skip(3).Take(480*channels).SequenceEqual(dry.Skip(3).Take(480*channels)) && buffer[0]==42 && buffer[^1]==42,$"{mode}/{rate}/{channels}: dry bypass and offset boundaries");
            state.Enabled=true; double difference=0;
            for(int block=0;block<100;block++)
            {
                provider.Read(buffer,3,480*channels); reference.Read(dry,3,480*channels);
                for(int i=3;i<3+480*channels;i++) { if(!float.IsFinite(buffer[i]) || Math.Abs(buffer[i])>1)throw new Exception("invalid sample"); difference+=Math.Abs(buffer[i]-dry[i]); }
            }
            Check(difference>1,"enabled effect changes signal with bounded finite output");
            input.Silent=true; provider.Read(buffer,3,480*channels); Check(buffer.Skip(3).Take(480*channels).Any(x=>Math.Abs(x)>.0001),"delay tail exists");
            provider.Reset(); provider.Read(buffer,3,480*channels); Check(buffer.Skip(3).Take(480*channels).All(x=>x==0),"seek/reset clears old delay audio");
            input.Silent=false; input.LeftOnly=true; provider.Reset();
            for(int block=0;block<50;block++)provider.Read(buffer,3,480*channels);
            if(channels==2)Check(Enumerable.Range(0,480).All(i=>buffer[4+i*2]==0),"stereo channels do not bleed");
            input.LeftOnly=false; state.Enabled=false;
            for(int block=0;block<10;block++)provider.Read(buffer,3,480*channels);
            input.Silent=true; provider.Read(buffer,3,480*channels); Check(buffer.Skip(3).Take(480*channels).All(x=>x==0),"bypass clears tails after ramp");
        }
        var settings=new MusicEffectsState { Enabled=true,MixPercent=double.NaN,Mode="invalid" };
        Check(settings.Options.Mix==0 && settings.Mode=="Flanger","invalid settings safely bounded");
        settings.MixPercent=1000; Check(settings.Options.Mix==.6,"maximum effect level capped");
        var signal=new Signal(48000,2) { Amplitude=8 }; var effect=new MusicEffectsSampleProvider(signal,()=>settings.Options); var samples=new float[960];
        for(int block=0;block<100;block++) { settings.Mode=MusicEffectsState.Modes[block%3]; effect.Read(samples,0,samples.Length); Check(samples.All(x=>float.IsFinite(x)&&Math.Abs(x)<=1),"live effect switch / overload bounded"); }
        signal.Amplitude=.5; settings.Mode="Reverb";
        for(int i=0;i<500;i++)effect.Read(samples,0,960);
        var bytes=GC.GetAllocatedBytesForCurrentThread(); var timer=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<3000;i++)effect.Read(samples,0,960);
        timer.Stop(); var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Check(allocated<1024,"steady audio callback has no per-block allocations");
        Console.WriteLine($"30 seconds stereo reverb processed in {timer.Elapsed.TotalMilliseconds:0} ms; allocations {allocated} bytes (includes stopwatch)");
    }
}
