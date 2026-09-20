using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HazzKaraokeHoster.App;
using HazzKaraokeHoster.Core.Models;

internal static class Check
{
    [STAThread]
    private static void Main(string[] args)
    {
        var early = new SingerQueueEntry { SingerName = "Early" };
        var newcomer = new SingerQueueEntry { SingerName = "New" };
        var held = new SingerQueueEntry { SingerName = "Held", IsHeld = true };
        var empty = new SingerQueueEntry { SingerName = "Empty" };
        foreach (var singer in new[] { early, newcomer, held }) singer.Songs.Add(new SingerSongEntry());
        var turns = new Dictionary<SingerQueueEntry, FairTurnRecord>
        {
            [early] = new() { Turns = 2, Arrived = 1, LastTurn = 5 },
            [newcomer] = new() { Turns = 0, Arrived = 8 },
            [held] = new() { Arrived = 2 },
            [empty] = new() { Arrived = 3 }
        };
        Require(FairRotationPolicy.Order(new[] { held, early, empty, newcomer }, x => turns[x]).SequenceEqual(new[] { newcomer, early, empty, held }), "Fair rotation readiness/turn ordering failed");
        turns[newcomer].Turns = 2; turns[newcomer].LastTurn = 9;
        Require(FairRotationPolicy.Order(new[] { newcomer, early }, x => turns[x])[0] == early, "Longest waiting tie-break failed");
        turns[newcomer].Turns = 0;
        Require(FairRotationPolicy.Order(new[] { newcomer, early }, x => turns[x], "Arrival order")[0] == early, "Arrival priority failed");
        Require(FairRotationPolicy.Order(new[] { newcomer, early }, x => turns[x], "Fewest turns", "Longest waiting", true, 9)[0] == early, "Consecutive turn protection failed");
        Require(FairRotationPolicy.Order(new[] { newcomer }, x => turns[x], "Fewest turns", "Longest waiting", true, 9)[0] == newcomer, "Only ready singer must remain available");
        var round = new RotationRoundState();
        turns[early].LastRound = 1;
        turns[newcomer].EligibleRound = 1;
        Require(RotationMethods.Order(new[] { early, newcomer }, x => turns[x], round, "Round robin", "End of current round", 2, "Longest waiting", false, 0)[0] == newcomer && round.Round == 1, "Round robin repeated a served singer");
        turns[newcomer].EligibleRound = 2;
        turns[early].LastRound = 0;
        Require(RotationMethods.Order(new[] { newcomer, early }, x => turns[x], round, "Closed rounds", "Next round", 2, "Longest waiting", false, 0)[0] == early && round.Round == 1, "Closed round admitted a deferred newcomer");
        turns[early].LastRound = 1;
        RotationMethods.Order(new[] { early, newcomer }, x => turns[x], round, "Closed rounds", "Next round", 2, "Longest waiting", false, 0);
        Require(round.Round == 2, "Completed round did not advance");
        turns[early].Group = turns[newcomer].Group = "Team";
        turns[early].LastRound = 2;
        RotationMethods.Order(new[] { early, newcomer }, x => turns[x], round, "Group rotation", "End of current round", 2, "Longest waiting", false, 0);
        Require(round.Round == 3, "Group members received multiple slots in a round");
        HazzKaraokeHoster.Playback.AudioNormalization.Enabled = true;
        HazzKaraokeHoster.Playback.AudioNormalization.TargetDb = -18;
        foreach (var inputGain in new[] { 0.08, 0.8 })
        {
            var tone = new NAudio.Wave.SampleProviders.SignalGenerator(48000, 2) { Gain = inputGain, Frequency = 440, Type = NAudio.Wave.SampleProviders.SignalGeneratorType.Sin };
            var normalizer = new HazzKaraokeHoster.Playback.NormalizingSampleProvider(tone);
            var samples = new float[9600];
            for (var block = 0; block < 300; block++)
            {
                normalizer.Read(samples, 0, samples.Length);
                Require(samples.All(v => float.IsFinite(v) && Math.Abs(v) <= 0.891252f), "Normalization peak limit failed");
            }
            var rms = Math.Sqrt(samples.Average(v => (double)v * v));
            Require(Math.Abs(20 * Math.Log10(rms) + 18) < 0.5, "Normalization did not converge to target");
        }
        HazzKaraokeHoster.Playback.AudioNormalization.Enabled = false;
        var bypass = new HazzKaraokeHoster.Playback.NormalizingSampleProvider(new NAudio.Wave.SampleProviders.SignalGenerator(48000, 2) { Gain = 0.25, Type = NAudio.Wave.SampleProviders.SignalGeneratorType.Sin });
        var flat = new float[100]; bypass.Read(flat, 0, flat.Length);
var original = new float[100]; new NAudio.Wave.SampleProviders.SignalGenerator(48000, 2) { Gain = 0.25, Type = NAudio.Wave.SampleProviders.SignalGeneratorType.Sin }.Read(original, 0, original.Length); Require(flat.SequenceEqual(original), "Disabled normalization altered fresh stream");
        var application = new Application();
        for (var i = 0; i < 25; i++)
        {
            var routed = new RoutedMusicElement { OutputDeviceId = "HAZZ-NONEXISTENT-TEST-ENDPOINT", Source = new Uri("C:/nonexistent-hazz-test.wav"), IsMuted = true };
            var failed = false;
            routed.RoutingFailed += _ => failed = true;
            routed.Play();
            Require(failed, "Missing output must report failure rather than play on default output");
            routed.Close();
        }

        var audience = new AudienceWindow { Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
        audience.Show();
        var image = (Image)audience.FindName("SingerBackgroundImage");
        var gif = (Image)audience.FindName("SingerBackgroundGif");
        var video = (MediaElement)audience.FindName("SingerBackgroundVideo");
        var musicVideo = (AudienceVideoSurface)audience.FindName("MusicVideoMedia");
        var cdg = (Image)audience.FindName("AudienceCdgImage");
        audience.SetCdgSmoothing(true);
        Require(RenderOptions.GetBitmapScalingMode(cdg) == BitmapScalingMode.HighQuality, "Smooth CDG scaling failed");
        audience.SetCdgSmoothing(false);
        Require(RenderOptions.GetBitmapScalingMode(cdg) == BitmapScalingMode.NearestNeighbor, "Crisp CDG scaling failed");
        var windowsVideo = (MediaElement)musicVideo.Children[0];
        var scroller = (Border)audience.FindName("ScrollerPanel");
        var next = (Border)audience.FindName("NextSingerPanel");

        foreach (var pair in new[]
        {
            (Name: "Fit", Stretch: Stretch.Uniform),
            (Name: "FillCrop", Stretch: Stretch.UniformToFill),
            (Name: "Stretch", Stretch: Stretch.Fill),
            (Name: "Center", Stretch: Stretch.None)
        })
        {
            audience.Apply(new AudienceOverlaySettings { BackgroundStretchMode = pair.Name });
            Require(image.Stretch == pair.Stretch && gif.Stretch == pair.Stretch && video.Stretch == pair.Stretch,
                $"Background mode {pair.Name} did not reach every background media layer.");
        }

        audience.Apply(new AudienceOverlaySettings
        {
            ScrollerPosition = "Top",
            NextSingerPosition = OverlayPosition.TopCenter
        });
        Require(scroller.VerticalAlignment == VerticalAlignment.Top && next.Margin.Top == 74 && next.Margin.Bottom == 30,
            "Top scroller placement or top Next Singer clearance is incorrect.");

        audience.Apply(new AudienceOverlaySettings
        {
            ScrollerPosition = "Bottom",
            NextSingerPosition = OverlayPosition.BottomCenter
        });
        Require(scroller.VerticalAlignment == VerticalAlignment.Bottom && next.Margin.Bottom == 74 && next.Margin.Top == 30,
            "Bottom scroller placement or bottom Next Singer clearance is incorrect.");
        Require(windowsVideo.IsMuted && windowsVideo.Volume == 0 && windowsVideo.Stretch == Stretch.Uniform,
            "VJ music-video output is not silent and screen-fitted.");
        foreach (var method in new[] { "ShowMusicVideo", "PauseMusicVideo", "ResumeMusicVideo", "SyncMusicVideo", "ClearMusicVideo" })
            Require(typeof(AudienceWindow).GetMethod(method) is not null, $"Audience VJ method is missing: {method}");
        audience.SetSingerRotation(new[] { new AudienceSingerDisplayItem { Position = 1, SingerName = "Test Singer", SongText = "Test Song" } }, true);
        audience.Apply(new AudienceOverlaySettings { ShowNextSinger = true, ScrollerEnabled = true });
        audience.ShowMusicVideo(Path.Combine(Path.GetTempPath(), "hazz-vj-overlay-check.mp4"), TimeSpan.Zero, playing: false);
        Require(musicVideo.Visibility == Visibility.Visible && next.Visibility == Visibility.Collapsed && scroller.Visibility == Visibility.Collapsed,
            "Singer lists or scroller still cover the audience music-video layer.");
        audience.ClearMusicVideo();
        var longName = "Alexandra Catherine Montgomery and Friends With A Very Long Singer Name";
        audience.SetSingerRotation(Enumerable.Range(1, 4).Select(i => new AudienceSingerDisplayItem { Position = i, SingerName = longName, SongText = "Example song" }).ToArray(), true);
        foreach (var size in new[] { (800, 600), (1920, 1080) })
        {
            audience.Width = size.Item1; audience.Height = size.Item2;
            audience.Apply(new AudienceOverlaySettings { ShowNextSinger = true, NextSingerFontSize = 192 });
            audience.UpdateLayout();
            audience.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            audience.UpdateLayout();
            var names = Walk((StackPanel)audience.FindName("NextSingersStack")).OfType<StrokeTextBlock>().Where(t => t.Text == longName).ToArray();
            Require(names.Length == 4 && names.All(t => t.TextWrapping == TextWrapping.Wrap && t.TextTrimming == TextTrimming.None), "Long singer names must wrap in full");
            var root = (Grid)audience.FindName("Root");
            foreach (var name in names)
            {
                var bounds = name.TransformToAncestor(root).TransformBounds(new Rect(name.RenderSize));
                Require(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= root.ActualWidth + 1 && bounds.Bottom <= root.ActualHeight + 1, "Large singer text extends beyond audience screen");
            }
        }
        foreach (var edge in new[] { "Top", "Bottom" })
        {
            audience.Apply(new AudienceOverlaySettings { ScrollerPosition = edge, ScrollerEdgeInset = 120,
                NextSingerPosition = edge == "Top" ? OverlayPosition.TopCenter : OverlayPosition.BottomCenter });
            Require((edge == "Top" ? scroller.Margin.Top : scroller.Margin.Bottom) == 120, "Scroller inset did not reach selected edge");
            Require((edge == "Top" ? next.Margin.Top : next.Margin.Bottom) == 194, "Singer panel must leave clearance for inset scroller");
        }
        var stroke = new StrokeTextBlock { Text = "Singer", FontSize = 64, Foreground = Brushes.White, Padding = new Thickness(4) };
        StrokeTextBlock.SetStroke(stroke, Brushes.Red); StrokeTextBlock.SetStrokeWidth(stroke, 6);
        stroke.Measure(new Size(500, 120)); stroke.Arrange(new Rect(0, 0, 500, 120));
        var rendered = new System.Windows.Media.Imaging.RenderTargetBitmap(500, 120, 96, 96, PixelFormats.Pbgra32);
        rendered.Render(stroke); var pixels = new byte[500 * 120 * 4]; rendered.CopyPixels(pixels, 500 * 4, 0);
        Require(Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[i*4+2] > 150 && pixels[i*4+1] < 50), "Stroke renderer did not draw red outline pixels");
        var outlineSettings = new AudienceOverlaySettings { TextStrokes = new()
        {
            ["Singer"] = new TextStrokeSettings { Enabled = true, Width = 4, Color = "#FFFF0000" },
            ["Rotation"] = new TextStrokeSettings { Enabled = true, Width = 2, Color = "#FFFF0000" },
            ["Venue"] = new TextStrokeSettings { Enabled = true, Width = 6, Color = "#FF0000FF" }
        }};
        audience.Apply(outlineSettings);
        var outlinedNames = Walk((StackPanel)audience.FindName("NextSingersStack")).OfType<StrokeTextBlock>().Where(t=>t.Text==longName).ToArray();
        Require(outlinedNames.Length == 4 && outlinedNames.All(t=>StrokeTextBlock.GetStrokeWidth(t)==4), "Singer outline did not reach all names");
        var scrollerRuns = ((StrokeTextBlock)audience.FindName("ScrollerText")).Inlines;
        Require(scrollerRuns.Any(r=>StrokeTextBlock.GetStrokeWidth(r)==2) && scrollerRuns.Any(r=>StrokeTextBlock.GetStrokeWidth(r)==6), "Rotation and venue outlines must remain independent");
        foreach (var outlineWidth in new[] { 0d, 2d, 8d })
        {
            var heading = new StrokeTextBlock { Text = "NEXT SINGERS", FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White, Padding = new Thickness(outlineWidth / 2) };
            StrokeTextBlock.SetStrokeWidth(heading, outlineWidth);
            heading.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var w = heading.DesiredSize.Width;
            // Simulate the fractional width lost during display/layout rounding.
            heading.Arrange(new Rect(0, 0, w - 0.75, heading.DesiredSize.Height));
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(w) + 2, 60, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(heading);
            var data = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(data, bitmap.PixelWidth * 4, 0);
            Require(Enumerable.Range(0, bitmap.PixelWidth * bitmap.PixelHeight).Any(i => i % bitmap.PixelWidth > w * 0.75 && data[i * 4] > 100), "NEXT SINGERS loses its final word at a rounded width");
        }
        var second = (Border)audience.FindName("SecondScrollerPanel");
        var firstText = (StrokeTextBlock)audience.FindName("ScrollerText");
        var secondText = (StrokeTextBlock)audience.FindName("SecondScrollerText");
        var karaokeVideo = (AudienceVideoSurface)audience.FindName("AudienceMedia");
        foreach (var stretch in new[] { false, true, false })
        {
            audience.Apply(new AudienceOverlaySettings { KaraokeSizing = stretch ? "Stretch" : "Fit" });
            Require(cdg.Stretch == (stretch ? Stretch.Fill : Stretch.Uniform), "CDG sizing not applied");
            Require(((MediaElement)karaokeVideo.Children[0]).Stretch == cdg.Stretch && karaokeVideo.StretchToFill == stretch, "Karaoke video sizing not applied");
            Require(windowsVideo.Stretch == Stretch.Uniform, "Karaoke sizing changed music video sizing");
        }
        foreach (var rotation in new[] { false, true })
        foreach (var message in new[] { false, true })
        {
            audience.Apply(new AudienceOverlaySettings { ScrollerRotationEnabled = rotation, ScrollerMessageEnabled = message, ScrollerText = "Venue only" });
            var content = string.Concat(firstText.Inlines.OfType<System.Windows.Documents.Run>().Select(x => x.Text));
            Require(content.Contains(longName) == rotation && content.Contains("Venue only") == message, "Scroller switches did not independently filter content");
            Require((scroller.Visibility == Visibility.Visible) == (rotation || message), "Empty primary scroller not hidden");
        }
        foreach (var edge in new[] { "Top", "Bottom" })
        foreach (var height in new[] { 300, 600, 1080 })
        {
            audience.Height = height;
            audience.Apply(new AudienceOverlaySettings { ScrollerPosition = edge, SecondScrollerEnabled = true, SecondScrollerText = "Drinks offers", ScrollerFontSize = 120, SecondScrollerFontSize = 120, ScrollerEdgeInset = 250, SecondScrollerInset = 250 });
            audience.UpdateLayout();
            audience.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            audience.UpdateLayout();
            Require(second.Visibility == Visibility.Visible && second.VerticalAlignment != scroller.VerticalAlignment, "Scrollers share an edge");
            var root = (Grid)audience.FindName("Root");
            var a = scroller.TransformToAncestor(root).TransformBounds(new Rect(scroller.RenderSize));
            var b = second.TransformToAncestor(root).TransformBounds(new Rect(second.RenderSize));
            Require(!a.IntersectsWith(b) && a.Top >= 0 && b.Top >= 0 && a.Bottom <= root.ActualHeight + 1 && b.Bottom <= root.ActualHeight + 1, "Scroller bands overlap or leave screen");
        }
        audience.Apply(new AudienceOverlaySettings { ScrollerEnabled = false, SecondScrollerEnabled = true, SecondScrollerText = "Independent" });
        audience.UpdateLayout();
        var previousLeft = Canvas.GetLeft(secondText);
        System.Threading.Thread.Sleep(35);
        typeof(AudienceWindow).GetMethod("TickScroller", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(audience, null);
        Require(scroller.Visibility == Visibility.Collapsed && Canvas.GetLeft(secondText) < previousLeft, "Second scroller stops when primary disabled");
        audience.SetKaraokeActive(true);
        Require(second.Visibility == Visibility.Collapsed, "Second scroller covers karaoke");
        audience.SetKaraokeActive(false);
        Require(second.Visibility == Visibility.Visible, "Second scroller not restored after karaoke");
        audience.Apply(new AudienceOverlaySettings { SecondScrollerEnabled = true, SecondScrollerText = " " });
        Require(second.Visibility == Visibility.Collapsed, "Blank second message should hide its band");
        Console.WriteLine("PASS: independent scrollers, edge/overlap safety, secondary-only motion, karaoke sizing and retained audience behavior.");
        audience.VideoSyncOffsetSeconds = 1.25;
        Require(audience.AdjustedVideoPosition(TimeSpan.FromSeconds(10)).TotalSeconds == 11.25, "Positive AVS must advance video");
        audience.VideoSyncOffsetSeconds = -1.25;
        Require(audience.AdjustedVideoPosition(TimeSpan.FromSeconds(10)).TotalSeconds == 8.75 && audience.AdjustedVideoPosition(TimeSpan.Zero) == TimeSpan.Zero, "Negative AVS/clamping failed");
        audience.VideoSyncOffsetSeconds = 0;
        var backgroundPixels = Enumerable.Range(0,16).SelectMany(_ => new byte[]{0,0,255,255}).ToArray();
        var red = System.Windows.Media.Imaging.BitmapSource.Create(4,4,96,96,PixelFormats.Bgra32,null,backgroundPixels,16);
        var artPath = Path.Combine(Path.GetTempPath(),"hazz-v17-"+Guid.NewGuid()+".png");
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(red));using(var file=File.Create(artPath)) encoder.Save(file);
        var transparent = System.Windows.Media.Imaging.BitmapSource.Create(4,4,96,96,PixelFormats.Bgra32,null,new byte[64],16);
        audience.Apply(new AudienceOverlaySettings { BackgroundImageEnabled=true, BackgroundImagePath=artPath, CdgPresentation=new() { Enabled=true, BackgroundOpacity=0.4, LyricsOpacity=0.7 } });
        audience.ShowCdg(transparent);audience.SetKaraokeActive(true);
        Require(image.Visibility==Visibility.Visible && image.Opacity==0.4 && cdg.Opacity==0.7, "Transparent CDG background/opacity not shown");
        audience.Apply(new AudienceOverlaySettings { BackgroundImageEnabled=true, BackgroundImagePath=artPath });
        Require(image.Visibility==Visibility.Collapsed && cdg.Opacity==1, "Original CDG mode not restored");
        audience.ClearKaraokeVisual();audience.SetKaraokeActive(false);
        audience.Apply(new AudienceOverlaySettings { BackgroundImageEnabled=true, BackgroundImagePath=artPath, ShowNextSinger=false, ScrollerEnabled=false });
        using (var preview = new AudiencePreview { GetAudience = () => audience })
        {
            var previewWindow = new Window { Content=preview, Width=640, Height=400, ShowActivated=false, ShowInTaskbar=false };
            previewWindow.Show();previewWindow.UpdateLayout();preview.Refresh();previewWindow.UpdateLayout();
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(640,400,96,96,PixelFormats.Pbgra32);bitmap.Render(preview);
            var previewPixels = new byte[640*400*4];bitmap.CopyPixels(previewPixels,640*4,0);
            Require(previewPixels[(180*640+300)*4+2]>200,"Preview did not show idle audience background");
            audience.Hide();preview.Refresh();previewWindow.UpdateLayout();
            Require(audience.PreviewScene.ActualWidth>0 && audience.PreviewScene.ActualHeight>0,"Preview-only scene layout incorrect");
            var hidden = new AudienceWindow();
            hidden.Apply(new AudienceOverlaySettings { BackgroundImageEnabled=true, BackgroundImagePath=artPath, ShowNextSinger=false, ScrollerEnabled=false });
            preview.GetAudience=()=>hidden;preview.Refresh();previewWindow.UpdateLayout();preview.Refresh();
            bitmap.Clear();bitmap.Render(preview);bitmap.CopyPixels(previewPixels,640*4,0);
            Require(previewPixels[(180*640+300)*4+2]>200,"Preview must show background without ever opening audience window");
            hidden.Apply(new AudienceOverlaySettings { BackgroundImageEnabled=true, BackgroundImagePath=artPath, BackgroundStretchMode="Stretch", ShowNextSinger=true, ScrollerEnabled=true, ScrollerText="WELCOME TO HAZZ KARAOKE", SecondScrollerEnabled=true, SecondScrollerText="VENUE OFFERS — ASK AT THE BAR" });
            hidden.SetSingerRotation(new[] { new AudienceSingerDisplayItem { Position=1, SingerName="Alex", SongText="Dancing Queen", PhotoPath=artPath }, new AudienceSingerDisplayItem { Position=2, SingerName="Sam", SongText="Your next song" } },true);
            var navyBytes=Enumerable.Range(0,16).SelectMany(_=>new byte[]{70,40,18,255}).ToArray();
            ((Image)hidden.FindName("SingerBackgroundImage")).Source=System.Windows.Media.Imaging.BitmapSource.Create(4,4,96,96,PixelFormats.Bgra32,null,navyBytes,16);
            Canvas.SetLeft((StrokeTextBlock)hidden.FindName("ScrollerText"),20);
            Canvas.SetLeft((StrokeTextBlock)hidden.FindName("SecondScrollerText"),20);
            hidden.PreviewScene.UpdateLayout();preview.Refresh();previewWindow.UpdateLayout();
            bitmap.Clear();bitmap.Render(preview);bitmap.CopyPixels(previewPixels,640*4,0);
            Require(Enumerable.Range(0,640*400).Count(i=>previewPixels[i*4]>180 && previewPixels[i*4+1]>180 && previewPixels[i*4+2]>180)>50,"Preview omitted singer/scroller text");
            Require(Walk((StackPanel)hidden.FindName("NextSingersStack")).OfType<Image>().Any(x=>x.Source is not null),"Singer photo missing from shared preview scene");
            if(args.Length>1) { var previewEncoder=new System.Windows.Media.Imaging.PngBitmapEncoder();previewEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var output=File.Create(args[1]);previewEncoder.Save(output); }
            hidden.Width=900;hidden.Height=500;hidden.Show();hidden.UpdateLayout();preview.Refresh();
            Require(double.IsNaN(hidden.PreviewScene.Width) && hidden.PreviewScene.ActualWidth>700,"Opening audience display must restore normal responsive layout");
            preview.Dispose();hidden.Close();
            previewWindow.Close();
        }
        File.Delete(artPath);
        Console.WriteLine("PASS: AVS calculations, transparent CDG background/opacity, full idle preview and hidden audience layout.");
        audience.Close();
        if (args.Contains("--audience-only")) return;

        var main = new MainWindow();
        typeof(MainWindow).GetMethod("ApplyHostTextScale", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, new object[] { 1.5 });
        var settingsFit = (Viewbox)main.FindName("SettingsAutoFit");
        settingsFit.Measure(new Size(1000, 1800));
        settingsFit.Arrange(new Rect(settingsFit.DesiredSize));
        settingsFit.UpdateLayout();
        var settingsBorder = (Border)settingsFit.Child;
        foreach (var controlName in new[] { "BackgroundGifSpeedSlider", "BackgroundStretchCombo", "ScrollerSpeedText" })
        {
            var control = (FrameworkElement)main.FindName(controlName);
            var bounds = control.TransformToAncestor(settingsBorder).TransformBounds(new Rect(control.RenderSize));
            Require(bounds.Left >= 0 && bounds.Right <= settingsBorder.ActualWidth && bounds.Bottom <= settingsBorder.ActualHeight,
                controlName + " is clipped at maximum GUI text size");
        }
        var playingField = typeof(MainWindow).GetField("_karaokePlaying", BindingFlags.Instance | BindingFlags.NonPublic)!;
        playingField.SetValue(main, true);
        var playTask = (Task)typeof(MainWindow).GetMethod("StartKaraokeSafeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, new object[] { false })!;
        Require(playTask.IsCompletedSuccessfully && (bool)playingField.GetValue(main)!, "Play must leave active karaoke uninterrupted");
        playingField.SetValue(main, false);
        typeof(MainWindow).GetMethod("ApplyHostTextScale", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, new object[] { 1.0 });
        main.Measure(new Size(1440, 840));
        main.Arrange(new Rect(0, 0, 1440, 840));
        var positionCombo = (ComboBox)main.FindName("PositionCombo");
        positionCombo.ApplyTemplate();
        var comboChrome = (Border)positionCombo.Template.FindName("ComboChrome", positionCombo);
        Require(IsDark(comboChrome.Background) && IsLight(positionCombo.Foreground),
            "Closed dropdown does not have high-contrast light text on dark steel.");
        var option = (ComboBoxItem)positionCombo.Items[0];
        option.ApplyTemplate();
        var itemChrome = (Border)option.Template.FindName("ItemChrome", option);
        Require(IsDark(itemChrome.Background) && IsLight(option.Foreground),
            "Opened dropdown option does not have high-contrast light text on a dark row.");
        var singerCombo = (ComboBox)main.FindName("SingerNameBox");
        singerCombo.ApplyTemplate();
        var editor = (TextBox)singerCombo.Template.FindName("PART_EditableTextBox", singerCombo);
        Require(IsDark(editor.Background) && IsLight(editor.Foreground) && editor.CaretBrush is not null,
            "Editable singer dropdown is not high contrast.");

        var buttons = Walk(main).OfType<Button>().ToArray();
        var play = buttons.First(x => Equals(x.Content, "▶ PLAY"));
        var pause = buttons.First(x => Equals(x.Content, "Ⅱ PAUSE"));
        var stop = buttons.First(x => Equals(x.Content, "■ STOP"));
        var load = buttons.First(x => Equals(x.Content, "LOAD NEXT SINGER"));
        Require(Dominant(play.Background, 'g') && Dominant(stop.Background, 'r') && Dominant(pause.Background, 'y') && Dominant(load.Background, 'b'),
            "Play, Stop, Pause and Load Next Singer do not have their semantic colours.");
        Require(new[] { BrushText(play.Background), BrushText(pause.Background), BrushText(stop.Background), BrushText(load.Background) }.Distinct().Count() == 4,
            "Primary function buttons still share the same colour.");
        play.Tag = "Active";
        play.ApplyTemplate();
        var illumination = (Border)play.Template.FindName("Illumination", play);
        Require(illumination.Opacity >= 0.24, "Active button does not brighten.");

        var kind = typeof(MainWindow).GetField("_searchMediaKind", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var update = typeof(MainWindow).GetMethod("UpdateSearchModeUi", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var overlay = (Border)main.FindName("SearchResultsOverlay");
        kind.SetValue(main, "Music");
        update.Invoke(main, null);
        Require(Grid.GetColumn(overlay) == 2, "Music search still covers Deck 1.");
        kind.SetValue(main, "MusicVideo");
        update.Invoke(main, null);
        Require(Grid.GetColumn(overlay) == 2 && ((Button)main.FindName("SearchMusicVideoButton")).Tag?.ToString() == "Active",
            "Music Video search mode is missing or does not keep both music decks available.");
        kind.SetValue(main, "Karaoke");
        update.Invoke(main, null);
        Require(Grid.GetColumn(overlay) == 0, "Karaoke search does not leave the singer rotation available.");

        var singleDeck = typeof(MainWindow).GetMethod("SetSingleDeckMode", BindingFlags.Instance | BindingFlags.NonPublic)!;
        singleDeck.Invoke(main, new object[] { true, false, false });
        var deck2Player = (Border)main.FindName("DeckBPlayerControlsPanel");
        var sideControls = (Border)main.FindName("DeckBSideListControlsPanel");
        var deck2Footer = (WrapPanel)main.FindName("DeckBPlaylistFooter");
        Require(deck2Player.Visibility == Visibility.Collapsed && sideControls.Visibility == Visibility.Visible && deck2Footer.Visibility == Visibility.Collapsed,
            "Single Deck mode did not replace the complete Deck 2 player with side-list controls.");
        var sideButtons = Walk(sideControls).OfType<Button>().Select(button => button.Content?.ToString()).ToHashSet();
        foreach (var label in new[] { "ADD FILES", "LOAD LIST…", "SAVE LIST", "SELECT ALL", "▲ MOVE UP", "▼ MOVE DOWN", "SEND TO DECK 1 →", "SHUFFLE LIST", "REMOVE SELECTED", "CLEAR LIST" })
            Require(sideButtons.Contains(label), $"Side-list control is missing: {label}");
        singleDeck.Invoke(main, new object[] { false, false, false });
        Require(deck2Player.Visibility == Visibility.Visible && sideControls.Visibility == Visibility.Collapsed && deck2Footer.Visibility == Visibility.Visible,
            "Normal mode did not restore the Deck 2 player.");

        Require(main.FindName("DeckAProgress") is Slider && main.FindName("DeckBProgress") is Slider,
            "Music decks do not expose draggable seek sliders.");
        Require(((Border)main.FindName("KaraokeDeckDropTarget")).AllowDrop,
            "The Karaoke Deck is not a drop target.");
        var deckType = typeof(MainWindow).GetNestedType("MusicDeckId", BindingFlags.NonPublic)!;
        var deck1 = Enum.Parse(deckType, "Deck1");
        typeof(MainWindow).GetField("_restoringMusicDeckQueues", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, true);
        var playlistA = (ListBox)main.FindName("DeckAPlaylist");
        var playlistB = (ListBox)main.FindName("DeckBPlaylist");
        Require(playlistA.ContextMenu!.Items.OfType<MenuItem>().Count() == 8 && playlistB.ContextMenu!.Items.OfType<MenuItem>().Count() == 8, "Both playlists need all context actions");
        var currentTrack = MusicQueueItem.FromPath("C:/test/current.mp3");
        var first = MusicQueueItem.FromPath("C:/test/first.mp3");
        var nextTrack = MusicQueueItem.FromPath("C:/test/next.mp3");
        playlistA.Items.Add(currentTrack); playlistA.Items.Add(first); playlistA.Items.Add(nextTrack);
        typeof(MainWindow).GetField("_deck1CurrentItem", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, currentTrack);
        playlistA.SelectedItems.Add(currentTrack); playlistA.SelectedItems.Add(nextTrack);
        typeof(MainWindow).GetMethod("QueueSelectedNext", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, new[] { deck1 });
        Require(ReferenceEquals(playlistA.Items[0], currentTrack) && ReferenceEquals(playlistA.Items[1], nextTrack), "Play next displaced current playback or failed to order the next track");
        playlistA.SelectedItems.Add(nextTrack);
        typeof(MainWindow).GetMethod("MoveSelectedToOtherDeck", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, new[] { deck1 });
        Require(playlistA.Items.Contains(currentTrack) && playlistB.Items.Contains(nextTrack) && !playlistA.Items.Contains(nextTrack), "Move must retain current track and move queued tracks");
        first.IsFavourite = true;
        var favouriteVisual = (FrameworkElement)playlistA.ItemTemplate.LoadContent(); favouriteVisual.DataContext = first;
        favouriteVisual.Measure(new Size(600,200)); favouriteVisual.Arrange(new Rect(0,0,600,200)); favouriteVisual.UpdateLayout();
        Require(Walk(favouriteVisual).OfType<TextBlock>().Any(x => x.Text == "★" && x.Foreground is SolidColorBrush b && b.Color == Colors.Gold && ((Border)x.Parent).Visibility == Visibility.Visible), "Favourite star is not yellow");
        playlistA.Items.Clear(); playlistB.Items.Clear();
        typeof(MainWindow).GetField("_deck1CurrentItem", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, null);
        var setPaused = typeof(MainWindow).GetMethod("SetDeckPaused", BindingFlags.Instance | BindingFlags.NonPublic)!;
        setPaused.Invoke(main, new[] { deck1, (object)true });
        Require(Equals(((Button)main.FindName("DeckAPauseButton")).Content, "▶ RESUME"), "Pause does not switch to Resume.");
        setPaused.Invoke(main, new[] { deck1, (object)false });
        Require(Equals(((Button)main.FindName("DeckAPauseButton")).Content, "Ⅱ PAUSE"), "Resume does not restore the Pause label.");
        var handoffSeconds = (double)typeof(MainWindow).GetField("KaraokeStopHandoffSeconds", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        Require(Math.Abs(handoffSeconds - 1.5) < 0.001, "Karaoke Fade Stop handoff is not fixed at 1.5 seconds.");
        Require(typeof(MainWindow).GetField("_audienceMusicVideoDeck", BindingFlags.Instance | BindingFlags.NonPublic) is not null &&
                typeof(MainWindow).GetMethod("ApplyCurrentMusicVideoToAudience", BindingFlags.Instance | BindingFlags.NonPublic) is not null,
            "Music-deck VJ routing is not wired to the audience display.");

        Console.WriteLine("PASS: colours, dropdowns, audience layout, unobstructed VJ video, Music Video search, search placement, side-list controls, seek sliders, Karaoke Deck drop target, pause/resume and 1.5-second handoff.");
        application.Shutdown();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool IsDark(Brush brush)
        => brush is SolidColorBrush solid && (solid.Color.R + solid.Color.G + solid.Color.B) < 240;

    private static bool IsLight(Brush brush)
        => brush is SolidColorBrush solid && (solid.Color.R + solid.Color.G + solid.Color.B) > 600;

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Walk(child)) yield return descendant;
    }

    private static string BrushText(Brush brush) => brush.ToString();

    private static bool Dominant(Brush brush, char colour)
    {
        if (brush is not SolidColorBrush solid) return false;
        var c = solid.Color;
        return colour switch
        {
            'r' => c.R > c.G && c.R > c.B,
            'g' => c.G > c.R && c.G > c.B,
            'b' => c.B > c.R && c.B > c.G,
            'y' => c.R > c.B && c.G > c.B,
            _ => false
        };
    }
}
