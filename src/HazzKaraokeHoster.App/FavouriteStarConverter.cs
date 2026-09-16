using System.Globalization;
using System.Windows.Data;

namespace HazzKaraokeHoster.App;

public sealed class FavouriteStarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string path && !string.IsNullOrWhiteSpace(path) && MusicFavourites.Default.Contains(path) ? "★" : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
