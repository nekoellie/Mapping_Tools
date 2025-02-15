﻿using System;
using System.Globalization;
using System.Windows.Data;
using Mapping_Tools.Classes.SystemTools;

namespace Mapping_Tools.Components.Domain {
    class DoubleToStringConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return ((double)value).ToString(CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            TypeConverters.TryParseDouble(value.ToString(), out double result, double.Parse(parameter.ToString()));
            return result;
        }
    }
}
