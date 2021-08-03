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
        private readonly double Power;
        public RootValueConverter(double power = 2)
        {
            Power = power;
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return SignIndependentPow(d, 1 / Power);
            }
            else if (value is float f)
            {
                return SignIndependentPow(f, 1 / Power);
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
                d = SignIndependentPow(d, Power);
                if (targetType == typeof(double)) return d;
                return (float)d;
            }
            else
            {
                return AvaloniaProperty.UnsetValue;
            }
        }

        private double SignIndependentPow(double val, double p)
        {
            return Math.CopySign(Math.Pow(Math.Abs(val), p), val);
        }
    }

    public class RoundingValueConverter : IValueConverter
    {
        private readonly int Digits;

        public RoundingValueConverter(int digits = 0)
        {
            Digits = digits;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double temp;
            if (value is double d) temp = d;
            else if (value is float f) temp = f;
            else throw new InvalidOperationException($"Can't round a value of type '{value.GetType().FullName}'");
            temp = Math.Round(temp, Digits);
            object res = null;
            if (targetType == typeof(double)) res = temp;
            else if (targetType == typeof(float)) res = (float)temp;
            if (res != null) return res;
            throw new InvalidOperationException($"Can't round into type '{targetType.FullName}'");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert(value, targetType, parameter, culture);
        }
    }
}
