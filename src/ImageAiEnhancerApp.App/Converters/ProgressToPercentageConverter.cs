using System.Globalization;
using System.Windows.Data;

namespace ImageAiEnhancerApp.App.Converters;

public sealed class ProgressToPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double progress)
        {
            return Math.Clamp(progress, 0d, 1d) * 100d;
        }

        return 0d;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percentage)
        {
            return Math.Clamp(percentage / 100d, 0d, 1d);
        }

        return 0d;
    }
}
