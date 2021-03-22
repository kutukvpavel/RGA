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
using SkiaSharp;
using System.Linq;

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
        private Label GLLabel;
        private Label CoordsLabel;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Spectrum3D = this.FindControl<Skia3DSpectrum>("Spectrum3D");
            Spectrum3D.UpdateSynchronizingObject = Program.UpdateSynchronizingObject;
            Spectrum3D.PropertyChanged += Spectrum3D_PropertyChanged;
            Spectrum3D.Background = SKColor.Parse("#464646");
            GLLabel = this.FindControl<Label>("GLLabel");
            CoordsLabel = this.FindControl<Label>("CoordsLabel");
        }

        #region UI events

        private void Spectrum3D_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "CoordinatesString") CoordsLabel.Content = e.NewValue;
        }

        private void OnRenderChecked(object sender, RoutedEventArgs e)
        {
            GLLabel.Background = SkiaCustomControl.OpenGLEnabled ? Brushes.Lime : Brushes.OrangeRed;
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

        private void OnTopViewClick(object sender, RoutedEventArgs e)
        {
            Spectrum3D.XRotate = 90;
            Spectrum3D.ZRotate = 0;
            Spectrum3D.XTranslate = 0;
            Spectrum3D.YTranslate = 0;
            Spectrum3D.ScalingFactor = (float)Spectrum3D.Bounds.Width / Program.Repositories.Max(x => x.Right - x.Left);
            Spectrum3D.ScanSpacing = 
                (float)Spectrum3D.Bounds.Height / (Program.Repositories.Max(x => x.Results.Count) * Spectrum3D.ScalingFactor);
            Spectrum3D.ZScalingFactor = Skia3DSpectrum.ScalingLowerLimit;
        }

        private void OnOpenInGnuPlotClick(object sender, RoutedEventArgs e)
        {
            Program.GnuPlotInstance.HoldOn();
            foreach (var item in Program.Repositories)
            {
                var data = item.Get3DPoints();
                Program.GnuPlotInstance.SPlot(
                    data.Item1.Take(100000).ToArray(), 
                    data.Item2.Take(100000).ToArray(), 
                    data.Item3.Take(100000).ToArray(),
                    "pause -1");
            }
            Program.GnuPlotInstance.HoldOff();
        }

        #endregion
    }
}
