namespace EfMigrationManager.App.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

[ValueConversion(typeof(string), typeof(Brush))]
public sealed class ColorHintToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> _map = new()
    {
        ["Red"]       = new SolidColorBrush(Color.FromRgb(255, 80, 80)),
        ["Yellow"]    = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
        ["LimeGreen"] = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        ["Cyan"]      = new SolidColorBrush(Color.FromRgb(0, 188, 212)),
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && _map.TryGetValue(s, out var brush)
           ? brush
           : (Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush
              ?? Brushes.White);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
