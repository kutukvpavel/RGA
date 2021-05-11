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
using Avalonia.Data.Converters;
using System;

namespace _3DSpectrumVisualizer
{
    public class MainWindow : Window
    {
        public static IValueConverter AMUValueConverter { get; set; }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            GLLabel.Background = SkiaCustomControl.OpenGLEnabled ? Brushes.Lime : Brushes.OrangeRed;
            var backgroundColorButton = this.FindControl<AvaloniaColorPicker.ColorButton>("BackgroundPicker");
            backgroundColorButton.Color = (Color)Skia3DSpectrum.ColorConverter.Convert(
                Spectrum3D.Background, typeof(Color), null, CultureInfo.CurrentCulture);
        }

        public void InvalidateSpectrum(object sender, EventArgs e)
        {
            Spectrum3D.InvalidateVisual();
            SectionPlot.InvalidateVisual();
        }

        private static CsvConfiguration DumpConfig = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            Delimiter = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ";" : ","
        };
        private const string BackgroundSerializationName = "background";

        private Skia3DSpectrum Spectrum3D;
        private CheckBox LogarithmicIntensity;
        private Label GLLabel;
        private Label CoordsLabel;
        private ListBox LstColors;
        private bool ViewStateTop = false;
        private Slider LightEmulation;
        private float[] Last3DCorrds = new float[] { 10, 10, 15, 0, 45, 4, 0.01f, 0.1f };
        private SkiaSectionPlot SectionPlot;
        private Label SectionCoords;
        private Slider SectionAMUSlider;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Spectrum3D = this.FindControl<Skia3DSpectrum>("Spectrum3D");
            Spectrum3D.UpdateSynchronizingObject = Program.UpdateSynchronizingObject;
            Spectrum3D.DataRepositories = Program.Repositories;
            Spectrum3D.PropertyChanged += Spectrum3D_PropertyChanged;
            Spectrum3D.PointerPressed += Spectrum3D_PointerPressed;
            Spectrum3D.Background = Program.Deserialize(BackgroundSerializationName, SKColor.Parse("#0E0D0D"),
                Program.ColorSerializationConverter);
            GLLabel = this.FindControl<Label>("GLLabel");
            CoordsLabel = this.FindControl<Label>("CoordsLabel");
            LstColors = this.FindControl<ListBox>("lstColors");
            LightEmulation = this.FindControl<Slider>("sldLight");
            LightEmulation.Value = DataRepository.LightGradient[1].Alpha;
            LogarithmicIntensity = this.FindControl<CheckBox>("chkLog10");
            LogarithmicIntensity.Click += OnLogarithmicChecked;
            SectionPlot = this.FindControl<SkiaSectionPlot>("SpectrumSection");
            SectionPlot.DataRepositories = Program.Repositories;
            SectionPlot.PropertyChanged += SectionPlot_PropertyChanged;
            SectionPlot.AMURoundingDigits = DataRepository.AMURoundingDigits;
            SectionCoords = this.FindControl<Label>("lblSectionCoords");
            SectionAMUSlider = this.FindControl<Slider>("SectionAMUSlider");
            SectionAMUSlider.PropertyChanged += SectionAMUSlider_PropertyChanged;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Program.Serialize(Spectrum3D.Background, BackgroundSerializationName, Program.ColorSerializationConverter);
        }

        #region UI events

        private void SectionAMUSlider_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == Slider.ValueProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange) return;
                SectionPlot.InvalidateVisual();
            }
        }

        private void OnTimeAxisSliderChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == Slider.ValueProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange) return;
                try
                {
                    Spectrum3D.TimeAxisInterval = (float)(double)e.NewValue;
                    Spectrum3D.InvalidateVisual();
                }
                catch (Exception)
                {

                }
            }
        }

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
                    SectionPlot.InvalidateVisual();
                }
                catch (Exception)
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
                    SectionPlot.Background = Spectrum3D.Background;
                    Spectrum3D.InvalidateVisual();
                    SectionPlot.InvalidateVisual();
                }
                catch (Exception)
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
            SectionPlot.InvalidateVisual();
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
                catch (Exception)
                {

                }
            }
        }

        private void SectionPlot_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "CoordinatesString") SectionCoords.Content = e.NewValue;
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

        private void OnSectionAutoscaleXClick(object sender, RoutedEventArgs e)
        {
            SectionPlot.AutoscaleX();
        }

        private void OnSectionAutoscaleYClick(object sender, RoutedEventArgs e)
        {
            SectionPlot.AutoscaleY();
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
            float shiftX = Spectrum3D.FontPaint.TextSize * Spectrum3D.FontPaint.TextScaleX * 10;
            float extraY = Spectrum3D.FontPaint.TextSize * 6;
            Spectrum3D.XRotate = 90;
            Spectrum3D.YRotate = 0;
            Spectrum3D.ZRotate = 0;
            Spectrum3D.XTranslate = -shiftX * 8;
            Spectrum3D.YTranslate = 0;
            Spectrum3D.ScalingFactor = (float)Spectrum3D.Bounds.Width / 
                (Program.Repositories.Max(x => x.Right - x.Left) + shiftX);
            Spectrum3D.ScanSpacing = (float)(Spectrum3D.Bounds.Height - extraY * Spectrum3D.ScalingFactor) / 
                ((Program.Repositories.Max(x => x.Duration)) * Spectrum3D.ScalingFactor);
            Spectrum3D.ZScalingFactor = Skia3DSpectrum.ScalingLowerLimit;
            Spectrum3D.InvalidateVisual();
            ViewStateTop = true;
        }

        #endregion
    }
}
