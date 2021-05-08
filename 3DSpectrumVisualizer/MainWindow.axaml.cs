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

        public void InvalidateSpectrum(object sender, System.EventArgs e)
        {
             Spectrum3D.InvalidateVisual();
        }

        private static CsvConfiguration DumpConfig = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            Delimiter = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ";" : ","
        };

        private Skia3DSpectrum Spectrum3D;
        private CheckBox LogarithmicIntensity;
        private Label GLLabel;
        private Label CoordsLabel;
        private ListBox LstColors;
        private bool ViewStateTop = false;
        private Slider LightEmulation;
        private float[] Last3DCorrds = new float[] { 10, 10, 15, 0, 45, 4, 0.01f, 0.1f };

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Spectrum3D = this.FindControl<Skia3DSpectrum>("Spectrum3D");
            Spectrum3D.UpdateSynchronizingObject = Program.UpdateSynchronizingObject;
            Spectrum3D.DataRepositories = Program.Repositories;
            Spectrum3D.PropertyChanged += Spectrum3D_PropertyChanged;
            Spectrum3D.PointerPressed += Spectrum3D_PointerPressed;
            Spectrum3D.Background = SKColor.Parse("#0E0D0D");
            GLLabel = this.FindControl<Label>("GLLabel");
            CoordsLabel = this.FindControl<Label>("CoordsLabel");
            LstColors = this.FindControl<ListBox>("lstColors");
            LightEmulation = this.FindControl<Slider>("sldLight");
            LightEmulation.Value = DataRepository.LightGradient[1].Alpha;
            LogarithmicIntensity = this.FindControl<CheckBox>("chkLog10");
            LogarithmicIntensity.Click += OnLogarithmicChecked;
        }

        #region UI events

        private void OnColorSchemeEdited(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == AvaloniaColorPicker.ColorButton.ColorProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange || LstColors.SelectedIndex < 0) return;
                try
                {
                    Program.ColorSchemes.SelectedItem[LstColors.SelectedIndex] = SKColor.Parse(((Color)e.NewValue).ToString());
                    foreach (var item in Program.Repositories)
                    {
                        item.RecalculateShader();
                    }
                    Spectrum3D.InvalidateVisual();
                }
                catch (System.Exception)
                {

                }
            }
        }

        private void OnBackgroundEdited(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == AvaloniaColorPicker.ColorButton.ColorProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange) return;
                try
                {
                    var s = ((Color)e.NewValue).ToString();
                    Spectrum3D.Background = SKColor.Parse(s);
                    Spectrum3D.InvalidateVisual();
                }
                catch (System.Exception)
                {
                    
                }
            }
        }

        private void OnLogarithmicChecked(object sender, RoutedEventArgs e)
        {
            bool c = (bool)LogarithmicIntensity.IsChecked;
            foreach (var item in Program.Repositories)
            {
                item.LogarithmicIntensity = c;
                item.RecalculateShader();
            }
            Spectrum3D.InvalidateVisual();
        }

        private void OnLightSliderChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == Slider.ValueProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange) return;
                try
                {
                    DataRepository.LightGradient[1] = new SKColor(
                        DataRepository.LightGradient[1].Red, DataRepository.LightGradient[1].Green,
                        DataRepository.LightGradient[1].Blue, (byte)(double)e.NewValue);
                    foreach (var item in Program.Repositories)
                    {
                        item.RecalculateShader();
                    }
                    Spectrum3D.InvalidateVisual();
                }
                catch (System.Exception)
                {

                }
            }
        }

        private void Spectrum3D_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "CoordinatesString") CoordsLabel.Content = e.NewValue;
        }

        private void Spectrum3D_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed) ViewStateTop = false;
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

        private void OnRestore3DViewClick(object sender, RoutedEventArgs e)
        {
            Spectrum3D.XTranslate = Last3DCorrds[0];
            Spectrum3D.YTranslate = Last3DCorrds[1];
            Spectrum3D.XRotate = Last3DCorrds[2];
            Spectrum3D.YRotate = Last3DCorrds[3];
            Spectrum3D.ZRotate = Last3DCorrds[4];
            Spectrum3D.ScalingFactor = Last3DCorrds[5];
            Spectrum3D.ZScalingFactor = Last3DCorrds[6];
            Spectrum3D.ScanSpacing = Last3DCorrds[7];
            Spectrum3D.InvalidateVisual();
            ViewStateTop = false;
        }

        private void OnTopViewClick(object sender, RoutedEventArgs e)
        {
            if (!ViewStateTop)
            {
                Last3DCorrds = new float[] {
                    Spectrum3D.XTranslate,
                    Spectrum3D.YTranslate,
                    Spectrum3D.XRotate,
                    Spectrum3D.YRotate,
                    Spectrum3D.ZRotate,
                    Spectrum3D.ScalingFactor,
                    Spectrum3D.ZScalingFactor,
                    Spectrum3D.ScanSpacing
                };
            }
            Spectrum3D.XRotate = 90;
            Spectrum3D.YRotate = 0;
            Spectrum3D.ZRotate = 0;
            Spectrum3D.XTranslate = 0;
            Spectrum3D.YTranslate = 0;
            Spectrum3D.ScalingFactor = (float)Spectrum3D.Bounds.Width / Program.Repositories.Max(x => x.Right - x.Left);
            Spectrum3D.ScanSpacing = 
                (float)Spectrum3D.Bounds.Height / (Program.Repositories.Max(x => x.Results.Count) * Spectrum3D.ScalingFactor);
            Spectrum3D.ZScalingFactor = Skia3DSpectrum.ScalingLowerLimit;
            Spectrum3D.InvalidateVisual();
            ViewStateTop = true;
        }

        #endregion
    }
}
