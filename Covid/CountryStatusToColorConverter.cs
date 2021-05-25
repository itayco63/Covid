using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;
using static Covid.CountryStatusEnum;

namespace Covid
{
    public class CountryStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((CountryStatus)value)
            {
                case CountryStatus.Suspect:
                    return Brushes.Red;
                case CountryStatus.Regular:
                    return Brushes.LimeGreen;
                case CountryStatus.Status:
                    return Brushes.Black;
                case CountryStatus.badData:
                    return Brushes.Blue;
                case CountryStatus.midSuspect:
                    return Brushes.Gold;
                default:
                    return Brushes.Yellow;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
