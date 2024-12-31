using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace EZHolodotNet.Core
{
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter != null && int.TryParse(parameter.ToString(), out int paramValue))
            {
                return intValue == paramValue?Visibility.Visible:Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibilityValue && parameter != null && int.TryParse(parameter.ToString(), out int paramValue))
            {
                if (visibilityValue == Visibility.Visible)
                {
                    return paramValue;
                }
                else
                {
                    return 0;
                }
            }

            return 0;
        }
    }
}
