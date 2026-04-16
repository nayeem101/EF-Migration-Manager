namespace EfMigrationManager.App.Converters;

using System.Globalization;
using System.Windows.Data;
using EfMigrationManager.Core.Models;
using Wpf.Ui.Controls;

[ValueConversion(typeof(NotificationSeverity), typeof(InfoBarSeverity))]
public sealed class NotificationSeverityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            NotificationSeverity.Success       => InfoBarSeverity.Success,
            NotificationSeverity.Warning       => InfoBarSeverity.Warning,
            NotificationSeverity.Error         => InfoBarSeverity.Error,
            _                                  => InfoBarSeverity.Informational
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
