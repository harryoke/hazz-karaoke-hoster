using System.Windows.Controls;

namespace HazzKaraokeHoster.App;
public sealed class SingerColumnWidth
{
    public double Value { get; set; }
    public string Unit { get; set; } = "Pixel";
}
public static class SingerColumnLayout
{
    public static Dictionary<string,SingerColumnWidth> Capture(DataGrid grid)
    {
        var result=new Dictionary<string,SingerColumnWidth>();
        foreach(var column in grid.Columns)
        {
            var key=column.Header?.ToString(); var width=column.Width;
            if(string.IsNullOrEmpty(key))continue;
            // Auto/star values are sizing rules, not the widths the host sees. Restoring
            // those rules lets startup rows/viewport measurements redistribute columns.
            // Persist resolved device-independent pixels after layout instead.
            var actual=column.ActualWidth;
            if(grid.ActualWidth>0 && double.IsFinite(actual) && actual>0)
                result[key]=new SingerColumnWidth { Value=actual,Unit="Pixel" };
            else if(double.IsFinite(width.Value))
                result[key]=new SingerColumnWidth { Value=width.Value,Unit=width.UnitType.ToString() };
        }
        return result;
    }
    public static void Apply(DataGrid grid,Dictionary<string,SingerColumnWidth>? saved)
    {
        if(saved is null)return;
        foreach(var column in grid.Columns)
        {
            var key=column.Header?.ToString();
            if(key is null || !saved.TryGetValue(key,out var width) || width is null || !double.IsFinite(width.Value) || width.Value<0 || width.Value>10000)continue;
            if(!Enum.TryParse<DataGridLengthUnitType>(width.Unit,out var unit) || !Enum.IsDefined(unit))continue;
            if(unit==DataGridLengthUnitType.Star && width.Value<=0)continue;
            column.Width=new DataGridLength(width.Value,unit);
        }
    }
}
