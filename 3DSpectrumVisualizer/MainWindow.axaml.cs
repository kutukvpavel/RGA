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
                int i = 0;
                foreach (var item in Program.Config.RenderSensorProfiles)
                {
                    SectionPlot.RenderSensorProfiles.Add(new SensorVisibility() { Index = i++, Visible = item });
                }
                ColorPositionSlider.SmallChange = Program.Config.ColorPositionSliderPrecision;
                AutoupdateXScaleCheckbox.IsChecked = Program.Config.AutoupdateXScale;
                GLLabel.Background = SkiaCustomControl.OpenGLEnabled ? Brushes.Lime : Brushes.OrangeRed;
                Spectrum3D.Background = Program.Config.SpectraBackground;
                Spectrum3D.TimeAxisInterval = Program.Config.LastTimeAxisInterval;
                Spectrum3D.FastMode = Program.Config.FastMode;
                LastViewCoords.Add(ViewStates._3D, Program.Config.Last3DCoords);
                Load3DCords(ViewStates._3D);
                OnRestore3DViewClick(this, null);
                this.FindControl<Expander>("expLeft").IsExpanded = Program.Config.LeftPanelVisible;
            };
            this.Closing += (s, e) =>
            {
                Program.Config.RenderSensorProfiles = SectionPlot.RenderSensorProfiles.Select(x => x.Visible).ToArray();
                Program.Config.ShowGasRegions = SectionPlot.RenderGasRegions;
                Program.Config.ShowTemperatureProfile = SectionPlot.RenderTemperatureProfile;
                Program.Config.AutoupdateXScale = AutoupdateXScaleCheckbox.IsChecked.Value;
                Program.Config.LastAMUSection = SectionPlot.AMU;
                Program.Config.LastTimeAxisInterval = Spectrum3D.TimeAxisInterval;
                Program.Config.SpectraBackground = Spectrum3D.Background;
                Program.Config.FastMode = Spectrum3D.FastMode;
                Save3DCoords();
                Program.Config.Last3DCoords = LastViewCoords[ViewStates._3D];
                Program.Config.LeftPanelVisible = this.FindControl<Expander>("expLeft").IsExpanded;
            };
        }

        #region Private

        private enum ViewStates
        {
            _3D,
            Top,
            Front
        }

        private Dictionary<ViewStates, float[]> LastViewCoords = new Dictionary<ViewStates, float[]>();
        private int ExportSectionReentrancyTracker = 0;
        private Button ExportSectionButton;
        private Skia3DSpectrum Spectrum3D;
        private CheckBox LogarithmicIntensity;
        private Label GLLabel;
        private Label CoordsLabel;
        private ListBox LstColors;
        private ViewStates ViewState = ViewStates._3D;
        private Slider LightEmulation;
        private SkiaSectionPlot SectionPlot;
        private Label SectionCoords;
        private Slider SectionAMUSlider;
        private CheckBox HorizontalGradient;
        private Label LoadingLabel;
        private CheckBox AutoupdateXScaleCheckbox;
        private Slider ColorPositionSlider;
        private Slider HideFirstSlider;
        private Slider HideLastSlider;
        private ListBox SensorVisibleList;

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
            LightEmulation.Value = DataRepositoryBase.LightGradient[1].Alpha;
            LogarithmicIntensity = this.FindControl<CheckBox>("chkLog10");
            LogarithmicIntensity.Click += OnLogarithmicChecked;
            SectionPlot = this.FindControl<SkiaSectionPlot>("SpectrumSection");
            SectionPlot.DataRepositories = Program.Repositories;
            SectionPlot.PropertyChanged += SectionPlot_PropertyChanged;
            SectionPlot.AMURoundingDigits = DataRepositoryBase.AMURoundingDigits;
            SectionCoords = this.FindControl<Label>("lblSectionCoords");
            SectionAMUSlider = this.FindControl<Slider>("SectionAMUSlider");
            SectionAMUSlider.PropertyChanged += SectionAMUSlider_PropertyChanged;
            HorizontalGradient = this.FindControl<CheckBox>("chkHorizontalGradient");
            LoadingLabel = this.FindControl<Label>("lblLoading");
            ExportSectionButton = this.FindControl<Button>("btnExportSection");
            AutoupdateXScaleCheckbox = this.FindControl<CheckBox>("chkAutoX");
            ColorPositionSlider = this.FindControl<Slider>("sldPosition");
            HideFirstSlider = this.Find<Slider>("sldHideStart");
            HideLastSlider = this.Find<Slider>("sldHideEnd");
            HideFirstSlider.PropertyChanged += HideFirstSlider_PropertyChanged;
            HideLastSlider.PropertyChanged += HideLastSlider_PropertyChanged;
            SensorVisibleList = this.FindControl<ListBox>("lstSensors");
        }

        private void Save3DCoords()
        {
            if (!LastViewCoords.ContainsKey(ViewState)) LastViewCoords.Add(ViewState, null);
            LastViewCoords[ViewState] = new float[] {
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

        private bool Load3DCords(ViewStates vs)
        {
            if (!LastViewCoords.ContainsKey(vs)) return false;
            Spectrum3D.XTranslate = LastViewCoords[vs][0];
            Spectrum3D.YTranslate = LastViewCoords[vs][1];
            Spectrum3D.XRotate = LastViewCoords[vs][2];
            Spectrum3D.YRotate = LastViewCoords[vs][3];
            Spectrum3D.ZRotate = LastViewCoords[vs][4];
            Spectrum3D.ScalingFactor = LastViewCoords[vs][5];
            Spectrum3D.ZScalingFactor = LastViewCoords[vs][6];
            Spectrum3D.ScanSpacing = LastViewCoords[vs][7];
            return true;
        }

        private void SetUpFrontView()
        {
            float max = Program.Repositories.Max(x => x.Max);
            float min;
            float extraY = 0;
            if (Program.Repositories.Any(x => x.LogarithmicIntensity))
            {
                max = MathF.Log10(max);
                min = MathF.Log10(Program.Repositories.Min(x => x.PositiveMin));
            }
            else
            {
                extraY = Spectrum3D.FontPaint.TextSize;
                min = Program.Repositories.Min(x => x.Min);
            }
            if (min < -extraY) extraY = extraY / 3;
            float shiftX = Spectrum3D.FontPaint.TextSize * Spectrum3D.FontPaint.TextScaleX;
            Spectrum3D.XRotate = 0;
            Spectrum3D.YRotate = 0;
            Spectrum3D.ZRotate = 0;
            Spectrum3D.ScalingFactor = (float)Spectrum3D.Bounds.Width /
                (Program.Repositories.Max(x => x.Right - x.Left) + shiftX);
            extraY *= Spectrum3D.ScalingFactor;
            Spectrum3D.XTranslate = -shiftX * Spectrum3D.ScalingFactor;
            Spectrum3D.ScanSpacing = float.Epsilon;
            Spectrum3D.ZScalingFactor = ((float)Spectrum3D.Bounds.Height - extraY) / (max - min) / Spectrum3D.ScalingFactor;
            Spectrum3D.YTranslate = (max + min) / 2 * Spectrum3D.ZScalingFactor * Spectrum3D.ScalingFactor - extraY;
        }

        private void SetUpTopView()
        {
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
        }

        #endregion

        #region Public Methods

        public void RepoInitCallback(object s, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                LoadingLabel.Background = Brushes.Yellow;
                string appTitle = Title;
                Title = "";
                foreach (var item in Program.Repositories)
                {
                    Title += $"{item.Location.Split(Path.DirectorySeparatorChar).LastOrDefault()}, ";
                }
                Title = Title.Remove(Title.Length - 2, 2);
                Title += $"\t--- {appTitle}";
            });
        }

        public void RepoLoadedCallback(object s, EventArgs e)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                bool allLog = Program.Repositories.All(x => x.LogarithmicIntensity);
                bool allLin = Program.Repositories.All(x => !x.LogarithmicIntensity);
                LogarithmicIntensity.IsChecked = (allLog ^ allLin) ? (bool?)allLog : null;
                SectionAMUSlider.Maximum = Program.Repositories.Max(x => x.Right);
                SectionAMUSlider.Minimum = Program.Repositories.Min(x => x.Left);
                SectionPlot.AMU = Program.Config.LastAMUSection;
                SectionPlot.AutoscaleX(false);
                SectionPlot.AutoscaleYForAllSections();
                LoadingLabel.IsVisible = false;
                SectionPlot.UpdateRepos();
                InvalidateSpectrum(this, null);
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

        private void HideLastSlider_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == Slider.ValueProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange) return;
                SectionPlot.HideLastPercentOfResults = (float)(double)e.NewValue;
            }
        }

        private void HideFirstSlider_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.Property == Slider.ValueProperty)
            {
                if (e.NewValue == null || !e.IsEffectiveValueChange) return;
                SectionPlot.HideFirstPercentOfResults = (float)(double)e.NewValue;
            }
        }

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
                await Task.Run(() => DataRepositoryBase.ExportSections(Program.Repositories, SectionPlot.AMU, path));
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
            FolderDataRepository.UseHorizontalGradient = (bool)HorizontalGradient.IsChecked;
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
                    FolderDataRepository.LightGradient[1] = FolderDataRepository.LightGradient[1].WithAlpha((byte)(double)e.NewValue);
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
            if (e.GetCurrentPoint(null).Properties.IsRightButtonPressed) ViewState = ViewStates._3D;
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
                    using CsvWriter c = new CsvWriter(w, FolderDataRepository.ExportCsvConfig);
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
            Save3DCoords();
            Load3DCords(ViewStates._3D);
            Spectrum3D.InvalidateVisual();
            ViewState = ViewStates._3D;
        }

        private void OnTopViewClick(object sender, RoutedEventArgs e)
        {
            if (!Program.Repositories.Any()) return;
            Save3DCoords();
            if (ViewState != ViewStates.Top)
            {
                if (!Load3DCords(ViewStates.Top)) SetUpTopView();
            }
            else
            {
                SetUpTopView();
            }
            Spectrum3D.InvalidateVisual();
            ViewState = ViewStates.Top;
        }

        private void OnFrontViewClick(object sender, RoutedEventArgs e)
        {
            if (!Program.Repositories.Any()) return;
            Save3DCoords();
            if (ViewState != ViewStates.Front)
            {
                if (!Load3DCords(ViewStates.Front)) SetUpFrontView();
            }
            else
            {
                SetUpFrontView();
            }
            Spectrum3D.InvalidateVisual();
            ViewState = ViewStates.Front;
        }

        #endregion
    }
}
