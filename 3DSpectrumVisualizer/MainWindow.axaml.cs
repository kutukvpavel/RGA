using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Input;
using System.Diagnostics;
using CsvHelper;
using CsvHelper.Configuration;
using System.IO;
using System.Globalization;

namespace _3DSpectrumVisualizer
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private static CsvConfiguration DumpConfig = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            Delimiter = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ";" : ","
        };

        private Skia3DSpectrum Spectrum3D;
        private Skia2DWaterfall Waterfall2D;
        private Label GLLabel;
        private Label CoordsLabel;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Spectrum3D = this.FindControl<Skia3DSpectrum>("Spectrum3D");
            Spectrum3D.PropertyChanged += Spectrum3D_PropertyChanged;
            Waterfall2D = this.FindControl<Skia2DWaterfall>("Waterfall2D");
            GLLabel = this.FindControl<Label>("GLLabel");
            CoordsLabel = this.FindControl<Label>("CoordsLabel");
        }

        private void Spectrum3D_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "CoordinatesString") CoordsLabel.Content = e.NewValue;
        }

        private void OnRenderChecked(object sender, RoutedEventArgs e)
        {
            GLLabel.Background = SkiaCustomControl.OpenGLEnabled ? Brushes.Lime : Brushes.OrangeRed;
            Waterfall2D.SelectedRepositoryIndex = 0;
        }

        private void OnDumpDataClick(object sender, RoutedEventArgs e)
        {
            lock (Program.UpdateSynchronizingObject)
            {
                for (int i = 0; i < Program.Repositories.Count; i++)
                {
                    using TextWriter w = new StreamWriter(@$"E:\dump{i}.csv");
                    using CsvWriter c = new CsvWriter(w, DumpConfig);
                    foreach (var item in Program.Repositories[i].Results)
                    {
                        foreach (var point in item.Path2D.Points)
                        {
                            c.WriteField($"({point.X}:{point.Y})");
                        }
                        c.NextRecord();
                    }
                }
            }
        }
    }
}
