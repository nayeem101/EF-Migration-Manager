namespace EfMigrationManager.App.Converters;

using System.Globalization;
using System.Windows.Data;

[ValueConversion(typeof(object), typeof(bool))]
public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && !(value is string s && string.IsNullOrWhiteSpace(s));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
