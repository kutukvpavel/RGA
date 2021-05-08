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
    public class ColorScheme : ObservableCollection<SKColorObservableCollection>, INotifyPropertyChanged
    {
        private int _SelectedIndex = 0;

        public static IValueConverter StringConverter = new FuncValueConverter<SKColor, string>((x) => x.ToString());
        public static IValueConverter ColorConverter = new FuncValueConverter<SKColor, Color>(
            (x) => Color.FromArgb(x.Alpha, x.Red, x.Green, x.Blue));
        public static IValueConverter BrushConverter = new FuncValueConverter<SKColor, Brush>(
            (x) => new SolidColorBrush(Color.FromArgb(x.Alpha, x.Red, x.Green, x.Blue).ToUint32()));
        public static IValueConverter ArrayConverter = new FuncValueConverter<ObservableCollection<SKColor>, string[]>(
            (x) => x.Select(y => y.ToString()).ToArray());

        public new event PropertyChangedEventHandler PropertyChanged;

        public int SelectedIndex { get => _SelectedIndex + 1; 
            set { 
                _SelectedIndex = value - 1;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedIndex"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
            } 
        }
        public ObservableCollection<SKColor> SelectedItem { get => this[_SelectedIndex]; }
    }

    [JsonArray(ItemConverterType = typeof(SKColorJsonConverter))]
    public class SKColorObservableCollection : ObservableCollection<SKColor>
    { }
}
