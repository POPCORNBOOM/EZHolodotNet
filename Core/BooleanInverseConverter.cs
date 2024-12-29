using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace EZHolodotNet.Core
{
    /// <summary>
    /// 布尔值反转转换器，将true转换为false，false转换为true。
    /// </summary>
    public class BooleanInverseConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值反转。
        /// </summary>
        /// <param name="value">绑定源的值。</param>
        /// <param name="targetType">绑定目标的类型。</param>
        /// <param name="parameter">可选参数。</param>
        /// <param name="culture">区域性信息。</param>
        /// <returns>反转后的布尔值。</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            // 如果值不是布尔类型，可以根据需求返回默认值
            return false;
        }

        /// <summary>
        /// 将值反转回源类型。
        /// </summary>
        /// <param name="value">绑定目标的值。</param>
        /// <param name="targetType">绑定源的类型。</param>
        /// <param name="parameter">可选参数。</param>
        /// <param name="culture">区域性信息。</param>
        /// <returns>反转后的布尔值。</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            // 如果值不是布尔类型，可以根据需求返回默认值
            return false;
        }
    }
}
