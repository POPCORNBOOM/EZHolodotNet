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
            if (value is int intValue && parameter is string paramString)
            {
                // 将参数按 '|' 分割成多个值
                var paramValues = paramString
                    .Split('|')
                    .Select(p => int.TryParse(p, out var parsedValue) ? parsedValue : (int?)null)
                    .Where(p => p.HasValue)
                    .Select(p => p.Value)
                    .ToList();

                // 检查 value 是否在这些参数中
                if (paramValues.Contains(intValue))
                {
                    return Visibility.Visible;
                }
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
