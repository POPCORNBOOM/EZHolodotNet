using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace EZHolodotNet.Core
{
    public class IntToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter != null && int.TryParse(parameter.ToString(), out int paramValue))
            {
                // 当整数值等于参数时返回 true
                return intValue == paramValue;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter != null && int.TryParse(parameter.ToString(), out int paramValue))
            {
                // 如果布尔值为 true，返回参数值
                if (boolValue)
                {
                    return paramValue;
                }
                else
                {
                    // 如果布尔值为 false，返回一个默认值（例如 0）
                    return 0;
                }
            }

            return 0;
        }
    }
}
