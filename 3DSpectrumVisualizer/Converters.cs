using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Data.Converters;

namespace _3DSpectrumVisualizer
{
    public class RootValueConverter : IValueConverter
    {
        private double Power;
        public RootValueConverter(double power = 2)
        {
            Power = power;
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return Math.Pow(d, 1 / Power);
            }
            else if (value is float f)
            {
                return Math.Pow(f, 1 / Power);
            }
            else
            {
                return AvaloniaProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                d = Math.Pow(d, Power);
                if (targetType == typeof(double)) return d;
                return (float)d;
            }
            else
            {
                return AvaloniaProperty.UnsetValue;
            }
        }
    }
}
