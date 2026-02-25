using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ImageAiEnhancerApp.App.Converters;

public sealed class CompareLineMarginConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return new Thickness(0);
        }

        var width = values[0] is double w ? w : 0d;
        var position = values[1] is double p ? p : 0d;
        var left = (Math.Clamp(position, 0d, 1d) * width) - 1d;

        return new Thickness(Math.Max(0, left), 0, 0, 0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
