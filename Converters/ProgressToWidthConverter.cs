using System;
using System.Globalization;
using System.Windows.Data;

namespace SP.Converters
{
    public class ProgressToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double progress = (double)value;
            double maxWidth = parameter != null ? double.Parse(parameter.ToString()) : 100.0; // 기본 최대 너비
            return progress * maxWidth; // 예: 0.7 * 100 = 70
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
