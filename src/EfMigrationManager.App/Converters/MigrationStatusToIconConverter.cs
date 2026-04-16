namespace EfMigrationManager.App.Converters;

using System.Globalization;
using System.Windows.Data;
using EfMigrationManager.Core.Models;

[ValueConversion(typeof(MigrationStatus), typeof(string))]
public sealed class MigrationStatusToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MigrationStatus.Applied ? "✅" : "🟡";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
