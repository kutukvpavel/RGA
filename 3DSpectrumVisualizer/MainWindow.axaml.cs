using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using CsvHelper;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace _3DSpectrumVisualizer
{
    public class MainWindow : Window
    {
        public static IValueConverter AMUStringValueConverter { get; set; } 
            = new FuncValueConverter<double, string>(x => x.ToString("F1"));
        public static IValueConverter AMURoundingValueConverter { get; set; }
            = new RoundingValueConverter(1);
        public static IValueConverter PositionValueConverter { get; set; }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            this.Opened += (s, e) =>
            {
                SectionPlot.RenderTemperatureProfile = Program.Config.ShowTemperatureProfile;
                SectionPlot.RenderGasRegions = Program.Config.ShowGasRegions;
                ColorPositionSlider.SmallChange = Program.Config.ColorPositionSliderPrecision;
                AutoupdateXScaleCheckbox.IsChecked = Program.Config.AutoupdateXScale;
                GLLabel.Background = SkiaCustomControl.OpenGLEnabled ? Brushes.Lime : Brushes.OrangeRed;
                Spectrum3D.Background = Program.Config.SpectraBackground;
                Spectrum3D.TimeAxisInterval = Program.Config.LastTimeAxisInterval;
                Spectrum3D.FastMode = Program.Config.FastMode;
                Last3DCoords = Program.Config.Last3DCoords;
                OnRestore3DViewClick(this, null);
            };
            this.Closing += (s, e) =>
            {
                Program.Config.ShowGasRegions = SectionPlot.RenderGasRegions;
                Program.Config.ShowTemperatureProfile = SectionPlot.RenderTemperatureProfile;
                Program.Config.AutoupdateXScale = AutoupdateXScaleCheckbox.IsChecked.Value;
                Program.Config.LastAMUSection = SectionPlot.AMU;
                Program.Config.LastTimeAxisInterval = Spectrum3D.TimeAxisInterval;
                Program.Config.SpectraBackground = Spectrum3D.Background;
                Program.Config.FastMode = Spectrum3D.FastMode;
                Save3DCoords();
                Program.Config.Last3DCoords = Last3DCoords;
            };
        }

        #region Private

        private int ExportSectionReentrancyTracker = 0;
        private Button ExportSectionButton;
        private Skia3DSpectrum Spectrum3D;
        private CheckBox LogarithmicIntensity;
        private Label GLLabel;
        private Label CoordsLabel;
        private ListBox LstColors;
        private bool ViewStateTop = false;
        private Slider LightEmulation;
        private SkiaSectionPlot SectionPlot;
        private Label SectionCoords;
        private Slider SectionAMUSlider;
        private CheckBox HorizontalGradient;
        private Label LoadingLabel;
        private float[] Last3DCoords;
        private CheckBox AutoupdateXScaleCheckbox;
        private Slider ColorPositionSlider;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Spectrum3D = this.FindControl<Skia3DSpectrum>("Spectrum3D");
            Spectrum3D.UpdateSynchronizingObject = Program.UpdateSynchronizingObject;
            Spectrum3D.DataRepositories = Program.Repositories;
            Spectrum3D.PropertyChanged += Spectrum3D_PropertyChanged;
            Spectrum3D.PointerPressed += Spectrum3D_PointerPressed;
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
            HorizontalGradient = this.FindControl<CheckBox>("chkHorizontalGradient");
            LoadingLabel = this.FindControl<Label>("lblLoading");
            ExportSectionButton = this.FindControl<Button>("btnExportSection");
            AutoupdateXScaleCheckbox = this.FindControl<CheckBox>("chkAutoX");
            ColorPositionSlider = this.FindControl<Slider>("sldPosition");
        }

        #endregion

        #region Public Methods

        public void RepoInitCallback(object s, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                LoadingLabel.Background = Brushes.Yellow;
                Title = $"{Title}: ";
                foreach (var item in Program.Repositories)
                {
                    Title += $"{item.Folder.Split(Path.DirectorySeparatorChar).LastOrDefault()}, ";
                }
                Title = Title.Remove(Title.Length - 2, 2);
            });
        }

        public void RepoLoadedCallback(object s, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                bool allLog = Program.Repositories.All(x => x.LogarithmicIntensity);
                bool allLin = Program.Repositories.All(x => !x.LogarithmicIntensity);
                LogarithmicIntensity.IsChecked = (allLog ^ allLin) ? (bool?)allLog : null;
                SectionPlot.AMU = Program.Config.LastAMUSection;
                SectionPlot.AutoscaleX(false);
                SectionPlot.AutoscaleYForAllSections();
                LoadingLabel.IsVisible = false;
            });
        }

        public void InvalidateSpectrum(object sender, EventArgs e)
        {
            if (AutoupdateXScaleCheckbox.IsChecked == true) SectionPlot.AutoscaleX(false);
            Spectrum3D.InvalidateVisual();
            SectionPlot.InvalidateVisual();
        }

        #endregion

        #region UI events
        private void OnShowTempProfileClick(object sender, RoutedEventArgs e)
        {
            SectionPlot.InvalidateVisual();
        }

        private void OnShowGasRegionsClick(object sender, RoutedEventArgs e)
        {
            SectionPlot.InvalidateVisual();
        }

        private async void OnExportSectionClick(object sender, RoutedEventArgs e)
        {
            if (!Program.Repositories.Any(x => x.Sections.ContainsKey(SectionPlot.AMU))) return;
            SaveFileDialog sd = new SaveFileDialog()
            {
                DefaultExtension = "csv",
                Filters = new List<FileDialogFilter>()
                { new FileDialogFilter() { Extensions = new List<string>() { "csv" }, Name = "Comma-separated values" } }
            };
            if (Directory.Exists(Program.Config.LastExportDir)) sd.Directory = Program.Config.LastExportDir;
            string path = await sd.ShowAsync(this);
            if (path == null) return;
            ExportSectionReentrancyTracker++;
            var buttonContent = ExportSectionButton.Content;
            var buttonBrush = ExportSectionButton.Background;
            ExportSectionButton.Content = "Exporting...";
            ExportSectionButton.Background = Brushes.Gray;
            try
            {
                await Task.Run(() => DataRepository.ExportSections(Program.Repositories, SectionPlot.AMU, path));
            }
            catch (Exception ex)
            {
                Program.LogException(this, ex);
            }
            finally
            {
                if (--ExportSectionReentrancyTracker == 0)
                {
                    ExportSectionButton.Content = buttonContent;
                    ExportSectionButton.Background = buttonBrush;
                }
            }
        }

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
                    SectionPlot.TimeAxisInterval = Spectrum3D.TimeAxisInterval / 1.5f;
                    Spectrum3D.InvalidateVisual();
                    SectionPlot.InvalidateVisual();
                }
                catch (Exception)
                {

                }
            }
        }

        private void OnPositionSchemeEdited(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == Slider.ValueProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange || LstColors.SelectedIndex < 0) return;
                try
                {
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

        private void OnColorSchemeEdited(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == AvaloniaColorPicker.ColorButton.ColorProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange || LstColors.SelectedIndex < 0) return;
                try
                {
                    Program.Config.ColorSchemes.SelectedItem[LstColors.SelectedIndex].Color 
                        = new SKColor(((Color)e.NewValue).ToUint32());
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
                    Spectrum3D.Background = new SKColor(((Color)e.NewValue).ToUint32());
                    SectionPlot.Background = Spectrum3D.Background;
                    Spectrum3D.InvalidateVisual();
                    SectionPlot.InvalidateVisual();
                }
                catch (Exception)
                {
                    
                }
            }
        }

        private void OnHorizontalGradientChecked(object sender, RoutedEventArgs e)
        {
            DataRepository.UseHorizontalGradient = (bool)HorizontalGradient.IsChecked;
            foreach (var item in Program.Repositories)
            {
                item.RecalculateShader();
            }
            Spectrum3D.InvalidateVisual();
            SectionPlot.InvalidateVisual();
        }

        private void OnLogarithmicChecked(object sender, RoutedEventArgs e)
        {
            if (LogarithmicIntensity.IsChecked == null) return;
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
                    DataRepository.LightGradient[1] = DataRepository.LightGradient[1].WithAlpha((byte)(double)e.NewValue);
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
                    using CsvWriter c = new CsvWriter(w, DataRepository.ExportCsvConfig);
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
            Spectrum3D.XTranslate = Last3DCoords[0];
            Spectrum3D.YTranslate = Last3DCoords[1];
            Spectrum3D.XRotate = Last3DCoords[2];
            Spectrum3D.YRotate = Last3DCoords[3];
            Spectrum3D.ZRotate = Last3DCoords[4];
            Spectrum3D.ScalingFactor = Last3DCoords[5];
            Spectrum3D.ZScalingFactor = Last3DCoords[6];
            Spectrum3D.ScanSpacing = Last3DCoords[7];
            Spectrum3D.InvalidateVisual();
            ViewStateTop = false;
        }

        private void OnTopViewClick(object sender, RoutedEventArgs e)
        {
            if (!ViewStateTop) Save3DCoords();
            if (!Program.Repositories.Any()) return;
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
                (Program.Repositories.Max(x => x.Duration) * Spectrum3D.ScalingFactor);
            Spectrum3D.ZScalingFactor = Skia3DSpectrum.ScalingLowerLimit;
            Spectrum3D.InvalidateVisual();
            ViewStateTop = true;
        }

        #endregion

        private void Save3DCoords()
        {
            Last3DCoords = new float[] {
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
    }
}
