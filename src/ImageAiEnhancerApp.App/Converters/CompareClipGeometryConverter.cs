using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ImageAiEnhancerApp.App.Converters;

public sealed class CompareClipGeometryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3)
        {
            return Geometry.Empty;
        }

        var width = values[0] is double w ? w : 0d;
        var height = values[1] is double h ? h : 0d;
        var position = values[2] is double p ? p : 0d;

        if (width <= 0 || height <= 0)
        {
            return Geometry.Empty;
        }

        var clipWidth = Math.Clamp(position, 0d, 1d) * width;
        return new RectangleGeometry(new Rect(0, 0, clipWidth, height));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
