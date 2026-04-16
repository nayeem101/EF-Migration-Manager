namespace EfMigrationManager.App.Converters;

using System.Globalization;
using System.IO;
using System.Windows.Data;

[ValueConversion(typeof(string), typeof(string))]
public sealed class PathToShortDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return string.Empty;
        try
        {
            var file = Path.GetFileName(s);
            var parent = Path.GetFileName(Path.GetDirectoryName(s)) ?? string.Empty;
            return parent.Length > 0 ? $"{parent}\\{file}" : file;
        }
        catch { return s; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
