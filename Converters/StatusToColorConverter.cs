using System;
using System.Globalization;
using System.Windows.Data;
using WpfApp1.Models;

namespace WpfApp1.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PingStatus status)
            {
                return status switch 
                { 
                    PingStatus.Alive => System.Windows.Media.Brushes.LightGreen, 
                    PingStatus.Dead => System.Windows.Media.Brushes.Red, 
                    _ => System.Windows.Media.Brushes.LightBlue 
                };
            }
            return System.Windows.Media.Brushes.LightBlue;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
