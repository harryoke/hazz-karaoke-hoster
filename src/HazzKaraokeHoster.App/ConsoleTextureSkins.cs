using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HazzKaraokeHoster.App;

// Static, shared bitmap brushes: no animation, per-frame generation or disk reads during playback.
internal static class ConsoleTextureSkins
{
    private sealed record Theme(int Image, bool Light, string Edge, string Surface);
    private static readonly Dictionary<string, Theme> Themes = new()
    {
        ["AzureSteel"] = new(1,false,"#74BDD9","#102936"),
        ["RubySteel"] = new(2,false,"#E78888","#301416"),
        ["AmethystSteel"] = new(3,true,"#7055A2","#F0EBFA"),
        ["BronzeSteel"] = new(4,false,"#D7A777","#2A2018"),
        ["GoldSteel"] = new(5,false,"#DCCA86","#292419"),
        ["SilverSteel"] = new(6,true,"#667582","#EDF0F3"),
        ["GraphiteSteel"] = new(7,false,"#84929E","#14191F"),
        ["RoseMarble"] = new(8,true,"#AA577A","#FAF0F5"),
        ["OnyxMarble"] = new(9,false,"#C58EC2","#1A131E")
    };
    private static readonly Dictionary<string, (Brush Background, Brush Panel)> Cache = new();
    internal static bool Contains(string name) => Themes.ContainsKey(name);
    internal static bool IsLight(string name) => Themes.TryGetValue(name,out var theme) && theme.Light;
    private static SolidColorBrush Solid(string colour) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour)); b.Freeze(); return b; }
    internal static void Apply(Window window, string name)
    {
        var theme = Themes[name];
        if (!Cache.TryGetValue(name,out var brushes))
        {
            var image = new BitmapImage(); image.BeginInit();
            image.UriSource = new Uri($"pack://application:,,,/Hazz Karaoke Hoster;component/Assets/Skins/texture{theme.Image}.png",UriKind.Absolute);
            image.DecodePixelWidth = 1280; image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit(); image.Freeze();
            Brush Surface(double tint)
            {
                var drawing = new DrawingGroup();
                drawing.Children.Add(new ImageDrawing(image,new Rect(0,0,image.PixelWidth,image.PixelHeight)));
                var shade = Solid(theme.Light ? "#FFFFFF" : "#000000").Clone(); shade.Opacity = tint; shade.Freeze();
                drawing.Children.Add(new GeometryDrawing(shade,null,new RectangleGeometry(new Rect(0,0,image.PixelWidth,image.PixelHeight))));
                drawing.Freeze();
                var brush = new DrawingBrush(drawing) { Stretch = Stretch.UniformToFill }; brush.Freeze(); return brush;
            }
            brushes = (Surface(theme.Light ? .08 : .15),Surface(theme.Light ? .55 : .48));
            Cache[name] = brushes;
        }
        window.Background = brushes.Background;
        window.Resources["PanelBrush"] = brushes.Panel;
        window.Resources["Panel2Brush"] = Solid(theme.Surface);
        window.Resources["BorderBrush"] = Solid(theme.Edge);
        if (theme.Light) window.Resources["SkinInk_FFD34D"] = Solid("#805A00");
        foreach (var key in new[] { "SkinEdge_293440", "SkinEdge_44515F", "SkinEdge_40505F", "SkinEdge_657585" }) window.Resources[key] = Solid(theme.Edge);
        // Keep list cells opaque and uncluttered; texture belongs to the console casing.
        foreach (var key in new[] { "SkinSurface_0D131A", "SkinSurface_111A22", "SkinSurface_18222D", "SkinSurface_17202A" }) window.Resources[key] = Solid(theme.Surface);
    }
}
