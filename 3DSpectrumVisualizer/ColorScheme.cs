using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using SkiaSharp;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Linq;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace _3DSpectrumVisualizer
{
    public class ColorScheme : ObservableCollection<GradientColorObservableCollection>, INotifyPropertyChanged
    {
        private int _SelectedIndex = 0;

        public static Color NullColor { get; set; } = Colors.Black;
        public static IValueConverter StringConverter = new FuncValueConverter<GradientColor, string>((x) => x.Color.ToString());
        public static IValueConverter ColorConverter = new FuncValueConverter<GradientColor, Color>(
            (x) => {
                if (x == null) return NullColor;
                return Color.FromArgb(x.Color.Alpha, x.Color.Red, x.Color.Green, x.Color.Blue);
            });
        public static IValueConverter BrushConverter = new FuncValueConverter<GradientColor, Brush>(
            (x) => new SolidColorBrush(Color.FromArgb(x.Color.Alpha, x.Color.Red, x.Color.Green, x.Color.Blue).ToUint32()));
        public static IValueConverter ArrayConverter = new FuncValueConverter<ObservableCollection<GradientColor>, string[]>(
            (x) => x.Select(y => y.Color.ToString()).ToArray());
        public static IValueConverter PositionConverter = new FuncValueConverter<GradientColor, double>((x) => x?.Position ?? 0);

        public new event PropertyChangedEventHandler PropertyChanged;

        public int SelectedIndex { get => _SelectedIndex + 1; 
            set { 
                _SelectedIndex = value - 1;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedIndex"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
            } 
        }
        public ObservableCollection<GradientColor> SelectedItem { get => this[_SelectedIndex]; }
    }

    [JsonArray]
    public class GradientColorObservableCollection : ObservableCollection<GradientColor>
    { }

    public class GradientColor : INotifyPropertyChanged
    {
        [JsonConstructor]
        public GradientColor(SKColor color, float position)
        {
            _Color = color;
            _Position = position;
        }

        private SKColor _Color;
        [JsonConverter(typeof(SKColorJsonConverter))]
        public SKColor Color { 
            get => _Color; 
            set 
            { 
                _Color = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Color"));
            } 
        }

        private float _Position;
        public float Position
        {
            get => _Position;
            set
            {
                _Position = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Position"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
