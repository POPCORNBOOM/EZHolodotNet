using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace EZHolodotNet.Core
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 转换方法：将 bool 转换为 Visibility。
        /// </summary>
        /// <param name="value">绑定源的值（bool）。</param>
        /// <param name="targetType">绑定目标的类型。</param>
        /// <param name="parameter">可选参数，用于控制转换行为。</param>
        /// <param name="culture">区域性信息。</param>
        /// <returns>转换后的 Visibility 值。</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;

            if (value is bool)
            {
                boolValue = (bool)value;
            }

            // 可选参数控制是否反转
            if (parameter != null && parameter.ToString().Equals("I", StringComparison.OrdinalIgnoreCase))
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 转换回方法：将 Visibility 转换回 bool。
        /// </summary>
        /// <param name="value">绑定目标的值（Visibility）。</param>
        /// <param name="targetType">绑定源的类型。</param>
        /// <param name="parameter">可选参数，用于控制转换行为。</param>
        /// <param name="culture">区域性信息。</param>
        /// <returns>转换后的 bool 值。</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility)
            {
                bool boolValue = ((Visibility)value) == Visibility.Visible;

                // 可选参数控制是否反转
                if (parameter != null && parameter.ToString().Equals("Inverse", StringComparison.OrdinalIgnoreCase))
                {
                    boolValue = !boolValue;
                }

                return boolValue;
            }

            return false;
        }
    }
}
