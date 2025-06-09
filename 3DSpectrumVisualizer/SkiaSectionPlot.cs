using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace _3DSpectrumVisualizer
{
    class SkiaSectionPlot : SkiaCustomControl
    {
        public static string IntensityLabelFormat { get; set; }
        public static float TicksScale { get; set; } = 1;

        public SkiaSectionPlot() : base()
        {
            PointerMoved += SkiaSectionPlot_PointerMoved;
            PointerWheelChanged += SkiaSectionPlot_PointerWheelChanged;
            PointerPressed += SkiaSectionPlot_PointerPressed;
            AMUProperty.Changed.Subscribe((e) =>
            {
                if (!IsInitialized) return;
                if (e.IsEffectiveValueChange && e.NewValue.HasValue)
                {
                    AMU = e.NewValue.Value;
                }
            });
            RenderSensorProfiles.CollectionChanged += RenderSensorProfiles_CollectionChanged;
        }

        #region Properties
        public AvaloniaProperty<float> AMUProperty = AvaloniaProperty.Register<SkiaSectionPlot, float>("AMU",
            defaultBindingMode: BindingMode.TwoWay);
        public AvaloniaProperty<bool> AMUPresent = AvaloniaProperty.Register<SkiaSectionPlot, bool>("AMUPresent",
            defaultBindingMode: BindingMode.OneWay, defaultValue: false);

        public SKPaint FontPaint { get; set; } = new SKPaint()
        { 
            Color = SKColor.Parse("#ECE2E2"), StrokeWidth = 1, TextSize = 14.0f, TextScaleX = 1,
            IsAntialias = true
        };

        public ObservableCollection<SensorVisibility> RenderSensorProfiles { get; set; } = new ObservableCollection<SensorVisibility>();

        public bool RenderGasRegions { get; set; } = true;

        public bool RenderTemperatureProfile { get; set; } = true;

        public new SKColor Background
        {
            get => base.Background;
            set
            {
                var grayscale = value.Red * 0.299 + value.Green * 0.587 + value.Blue * 0.114;
                FontPaint.Color = SKColor.Parse(grayscale > 167 ? "#000000" : "#FFFFFF");
                base.Background = value;
            }
        }

        public float TimeAxisInterval { get; set; } = 2;

        public int AMURoundingDigits { get; set; } = 1;

        public bool DisableRendering { get; set; } = false;

        private float _AMU = 1;
        public float AMU
        {
            get => _AMU;
            set
            {
                if (_AMU == value) return;
                _AMU = value;
                SetValue(AMUProperty, _AMU);
                SetValue(AMUPresent, DataRepositories.Any(x => x.Sections.ContainsKey(_AMU)));
            }
        }

        public IEnumerable<DataRepositoryBase> DataRepositories { get; set; } = new List<DataRepositoryBase>();

        public float XTranslate { get; set; } = 0;
        public float YTranslate { get; set; } = 0;
        public float YTranslateSensors { get; set; } = 0;
        public float XScaling { get; set; } = 1;
        public float YScaling { get; set; } = 1;
        public float YScalingSensors { get; set; } = 1;

        private float _HideLast = 0;
        private float _HideFirst = 0;
        public float HideFirstPercentOfResults
        {
            get => _HideFirst;
            set
            {
                _HideFirst = value;
                InvalidateVisual();
            }
        }
        public float HideLastPercentOfResults
        {
            get => _HideLast;
            set
            {
                _HideLast = value;
                InvalidateVisual();
            }
        }
        #endregion

        #region Public Methods

        public void UpdateRepos()
        {
            foreach (var item in DataRepositories)
            {
                item.DataAdded += Item_DataAdded;
            }
            Item_DataAdded(this, null);
        }
        public void Autoscale()
        {
            AutoscaleX(false);
            AutoscaleY();
        }

        public void AutoscaleX(bool invalidate = true)
        {
            if (!DataRepositories.Any()) return;
            XScaling = (float)Bounds.Width / DataRepositories.Max(x => x.Duration);
            XTranslate = 0;
            if (invalidate) InvalidateVisual();
        }

        public void AutoscaleY(bool invalidate = true)
        {
            if (!DataRepositories.Any()) return;
            float max = DataRepositories.Max(x =>
            {
                if (x.Sections.ContainsKey(AMU))
                {
                    var path = x.LogarithmicIntensity ? x.Sections[AMU].LogPath : x.Sections[AMU].LinearPath;
                    return path.Bounds.Bottom;
                }
                else
                {
                    return 0;
                }
            });
            float min = DataRepositories.Min(x =>
            {
                if (x.Sections.ContainsKey(AMU))
                {
                    var path = x.LogarithmicIntensity ? x.Sections[AMU].LogPath : x.Sections[AMU].LinearPath;
                    return path.Bounds.Top;
                }
                else
                {
                    return 0;
                }
            });
            AutoscalingYEngine(min, max, out float yScale, out float yTranslate);
            YScaling = yScale;
            YTranslate = yTranslate;
            if (invalidate) InvalidateVisual();
        }
        public void AutoscaleYSensors(bool invalidate = true)
        {
            var nonEmpty = DataRepositories.Select(x => (x.SensorLogScale ? x.LogSensorProfiles : x.SensorProfiles).Where(y => !y.IsEmpty));
            if (!nonEmpty.Any())
            {
                if (invalidate) InvalidateVisual();
                return;
            }
            Dispatcher.UIThread.Post(() =>
            {
                nonEmpty = nonEmpty.Select(x => x.Where((y, i) => i < RenderSensorProfiles.Count ? RenderSensorProfiles[i].Visible : true)).Where(x => x.Any());
                if (nonEmpty.Any())
                {
                    float max = nonEmpty.Max(x => x.Max(y => y.Bounds.Bottom));
                    float min = nonEmpty.Min(x => x.Min(y => y.Bounds.Top));
                    if (max != min)
                    {
                        AutoscalingYEngine(min, max, out float yScale, out float yTranslate);
                        YScalingSensors = yScale;
                        YTranslateSensors = yTranslate;
                    }
                }
                if (invalidate) InvalidateVisual();
            });
        }

        public void AutoscaleYForAllSections(bool invalidate = true)
        {
            if (!DataRepositories.Any()) return;
            //MS
            float max = DataRepositories.Max(x => x.Max);
            float min = DataRepositories.Min(x => x.Min);
            AutoscalingYEngine(min, max, out float yScale, out float yTranslate);
            YScaling = yScale;
            YTranslate = yTranslate;
            //Sensors
            AutoscaleYSensors(invalidate);
        }

        #endregion

        #region Private

        private Point _LastPoint;
        private Point? _LastPressedPoint;

        private void RenderSensorProfiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    (item as SensorVisibility).PropertyChanged += SensorVisibility_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    (item as SensorVisibility).PropertyChanged -= SensorVisibility_PropertyChanged;
                }
            }
        }

        private void SensorVisibility_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.IsEffectiveValueChange)
            {
                (e.Sender as SensorVisibility).Visible = (bool)e.NewValue;
                InvalidateVisual();
            }
        }

        private void Item_DataAdded(object sender, EventArgs e)
        {
            int max = DataRepositories.Any() ? DataRepositories.Max(x => x.SensorProfiles.Any() ? x.SensorProfiles.Count : 0) : 0;
            while (max > RenderSensorProfiles.Count) RenderSensorProfiles.Add(
                new SensorVisibility()
                { 
                    Index = RenderSensorProfiles.Count,
                    Visible = true
                });
        }

        private void AutoscalingYEngine(float min, float max, out float yScale, out float yTranslate)
        {
            yScale = (float)Bounds.Height * 0.9f / (max - min);
            yTranslate = min * yScale - (float)Bounds.Height * 0.05f;
        }

        protected override string UpdateCoordinatesString()
        {
            return FormattableString.Invariant(
                $"Tr: ({XTranslate:F1}, {YTranslate:F1}); Sc: ({XScaling:F3}, {YScaling:F3})");
        }

        protected override CustomDrawOp PrepareCustomDrawingOperation()
        {
            if (DisableRendering) return null;
            return new DrawSectionPlot(this, (float)(_LastPressedPoint?.Y ?? Bounds.Height / 2));
        }

        private void SkiaSectionPlot_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _LastPressedPoint = e.GetCurrentPoint(this).Position;
            InvalidateVisual();
        }

        private void SkiaSectionPlot_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            var pos = point.Position;
            var delta = (float)(e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X) / 10;
            float correction;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    correction = YScalingSensors;
                    YScalingSensors += YScalingSensors * delta;
                    correction = YScalingSensors / correction;
                    YTranslateSensors *= correction;
                    YTranslateSensors += (correction - 1) * (float)(Bounds.Height - pos.Y);
                }
                else
                {
                    correction = YScaling;
                    YScaling += YScaling * delta;
                    correction = YScaling / correction;
                    YTranslate *= correction;
                    YTranslate += (correction - 1) * (float)(Bounds.Height - pos.Y);
                }
            }
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                correction = XScaling;
                XScaling += XScaling * delta;
                correction = XScaling / correction;
                XTranslate *= correction;
                XTranslate -= (correction - 1) * (float)pos.X;
            }
            RaiseCoordsChanged();
            InvalidateVisual();
            e.Handled = true;
        }

        private void SkiaSectionPlot_PointerMoved(object sender, PointerEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            var pos = point.Position;
            //Pan
            if (point.Properties.IsLeftButtonPressed)
            {
                XTranslate += (float)(pos.X - _LastPoint.X);
                float diff = (float)(pos.Y - _LastPoint.Y);
                if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    YTranslateSensors += diff;
                }
                else
                {
                    YTranslate += diff;
                    RaiseCoordsChanged();
                }
                _LastPressedPoint = pos;
                InvalidateVisual();
            }
            _LastPoint = pos;
            e.Handled = true;
        }

        #endregion

        #region Render

        private class DrawSectionPlot : CustomDrawOp
        {
            public static float HeightReduction { get; set; } = 0.95f;

            private readonly float XTr;
            private readonly float YTr;
            private readonly float XSc;
            private readonly float YSc;
            private readonly float YTrS;
            private readonly float YScS;
            private readonly float AMU;
            private readonly SKPaint FontPaint;
            private readonly float TimeAxisInterval;
            private readonly float ResultsBegin;
            private readonly float ResultsEnd;
            private readonly bool ShowGasRegions;
            private readonly bool ShowTemperatureProfile;
            private readonly List<bool> ShowSensors;
            private readonly float LastMouseY;
            private readonly IEnumerable<DataRepositoryBase> Data;
            private List<SKRect> ClipRects;
            private readonly bool AnySensors;

            public DrawSectionPlot(SkiaSectionPlot parent, float lastMouseY) : base(parent)
            {
                XTr = parent.XTranslate;
                YTr = parent.YTranslate + (float)parent.Bounds.Height /** 0.95f*/;
                XSc = parent.XScaling;
                YSc = parent.YScaling;
                YTrS = parent.YTranslateSensors + (float)parent.Bounds.Height /** 0.95f*/;
                YScS = parent.YScalingSensors;
                AMU = MathF.Round(parent.AMU, parent.AMURoundingDigits);
                Data = parent.DataRepositories.Where(x => x.Enabled);
                FontPaint = parent.FontPaint;
                TimeAxisInterval = parent.TimeAxisInterval;
                ResultsBegin = 1 - parent.HideFirstPercentOfResults;
                ResultsEnd = 1 - parent.HideLastPercentOfResults;
                ShowGasRegions = parent.RenderGasRegions;
                ShowTemperatureProfile = parent.RenderTemperatureProfile;
                ShowSensors = parent.RenderSensorProfiles.Select(x => x.Visible).ToList();
                LastMouseY = lastMouseY;
                /*foreach (var item in Data)
                {
                    foreach (var color in item.SensorColors)
                    {
                        color.PathEffect = SKPathEffect.CreateTrim(parent.HideFirstPercentOfResults, ResultsEnd);
                    }
                }*/
                ClipRects = Data.Select(item =>
                {
                    var path = item.LogarithmicIntensity ? item.Sections[AMU].LogPath : item.Sections[AMU].LinearPath;
                    return new SKRect(
                                path.Bounds.Right - path.Bounds.Width * ResultsBegin,
                                float.NegativeInfinity,
                                path.Bounds.Left + path.Bounds.Width * ResultsEnd,
                                float.PositiveInfinity);
                }).ToList();
                AnySensors = Data.Any(x => (x.SensorProfiles.Count > 0));
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                if (!Data.Any()) return;
                canvas.Clear(BackgroundColor);
                var h = canvas.LocalClipBounds.Height * HeightReduction;
                Exception lastError = null;
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    try
                    {
                        canvas.Translate(XTr, 0);
                        canvas.Scale(XSc, 1);
                        RenderRegions(canvas);
                        if (ShowTemperatureProfile)
                        {
                            var s = h / Data.Max(x => x.TemperatureProfile.Bounds.Height) * HeightReduction;
                            canvas.Translate(0, h + Data.Min(x => x.TemperatureProfile.Bounds.Top) * s);
                            canvas.Scale(1, -s);
                            RenderTemperatureProfile(canvas);
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                }
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    try
                    {
                        canvas.Translate(XTr, YTrS);
                        canvas.Scale(XSc, -YScS);
                        RenderSensorProfiles(canvas);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                }
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    canvas.Translate(XTr, YTr);
                    canvas.Scale(XSc, -YSc);
                    int i = 0;
                    foreach (var item in Data)
                    {
                        var path = item.LogarithmicIntensity ? item.Sections[AMU].LogPath : item.Sections[AMU].LinearPath;
                        if (ResultsEnd != 1 || ResultsBegin != 1)
                        {
                            canvas.ClipRect(new SKRect(ClipRects[i].Left, path.Bounds.Top, ClipRects[i].Right, path.Bounds.Bottom));
                        }
                        canvas.DrawPath(path, item.SectionPaint);
                        i++;
                    }
                }
                RenderTimeAxis(canvas);
                RenderIntensityAxis(canvas);
                if (lastError != null) throw lastError;
            }

            private void RenderTimeAxis(SKCanvas canvas)
            {
                int step = (int)MathF.Ceiling(FontPaint.TextSize * FontPaint.TextScaleX * TimeAxisInterval * 5);
                int ticks = (int)MathF.Ceiling(canvas.LocalClipBounds.Width / step);
                var min = Data.Min(x => x.StartTime);
                float tripleStroke = FontPaint.StrokeWidth * 3;
                float tickHeight = FontPaint.TextSize * 2 * TicksScale;
                for (int i = 0; i < ticks; i++)
                {
                    var x = MathF.FusedMultiplyAdd(i, step, FontPaint.StrokeWidth);
                    var s = min.AddSeconds((x - XTr) / XSc).ToLongTimeString();
                    canvas.DrawText(s, x + tripleStroke, FontPaint.TextSize, FontPaint);
                    canvas.DrawLine(x, 0, x, tickHeight, FontPaint);
                }
            }

            private void RenderIntensityAxis(SKCanvas canvas)
            {
                //MS
                float value = (YTr - LastMouseY) / YSc;
                if (Data.Any(x => x.LogarithmicIntensity)) value = MathF.Pow(10, value);
                string text = value.ToString(IntensityLabelFormat);
                canvas.DrawText(text, 0, LastMouseY + FontPaint.TextSize * 1.1f, FontPaint);
                canvas.DrawLine(0, LastMouseY, FontPaint.MeasureText(text) * 1.5f * TicksScale, LastMouseY, FontPaint);
                //Sensors
                if (!AnySensors) return;
                value = (YTrS - LastMouseY) / YScS;
                if (Data.Any(x => x.SensorLogScale)) value = MathF.Pow(10, value);
                text = value.ToString(IntensityLabelFormat);
                float labelWidth = FontPaint.MeasureText(text);
                canvas.DrawText(text, canvas.LocalClipBounds.Width - labelWidth * 1.1f, LastMouseY + FontPaint.TextSize * 1.1f, FontPaint);
                canvas.DrawLine(canvas.LocalClipBounds.Width - labelWidth * 1.5f * TicksScale, 
                    LastMouseY, canvas.LocalClipBounds.Width, LastMouseY, FontPaint);
            }

            private void RenderRegions(SKCanvas canvas)
            {
                int i = 0;
                foreach (var item in Data)
                {
                    if (ResultsEnd != 1 || ResultsBegin != 1)
                    {
                        canvas.ClipRect(new SKRect(ClipRects[i].Left, 0, ClipRects[i].Right, canvas.LocalClipBounds.Height));
                    }
                    if (ShowGasRegions)
                    {
                        foreach (var reg in item.GasProfile)
                        {
                            canvas.DrawRect(IRegion.GetRect(reg, 0, canvas.LocalClipBounds.Height), reg.Paint);
                        }
                    }
                    foreach (var reg in item.UVProfile)
                    {
                        canvas.DrawRect(IRegion.GetRect(reg, 0, canvas.LocalClipBounds.Height), item.UVRegionPaint);
                    }
                    i++;
                }
            }

            private void RenderTemperatureProfile(SKCanvas canvas)
            {
                int i = 0;
                foreach (var item in Data)
                {
                    if (ResultsEnd != 1 || ResultsBegin != 1)
                    {
                        canvas.ClipRect(new SKRect(ClipRects[i].Left, item.TemperatureProfile.Bounds.Top,
                            ClipRects[i].Right, item.TemperatureProfile.Bounds.Bottom));
                    }
                    canvas.DrawPath(item.TemperatureProfile, item.TemperaturePaint);
                    i++;
                }
            }

            private void RenderSensorProfiles(SKCanvas canvas)
            {
                int j = 0;
                foreach (var item in Data)
                {
                    if (ResultsEnd != 1 || ResultsBegin != 1)
                    {
                        canvas.ClipRect(new SKRect(ClipRects[j].Left, canvas.LocalClipBounds.Top, ClipRects[j].Right, canvas.LocalClipBounds.Bottom));
                    }
                    for (int i = 0; i < item.SensorProfiles.Count; i++)
                    {
                        if (!(i < ShowSensors.Count ? ShowSensors[i] : true)) continue;
                        canvas.DrawPath(item.SensorLogScale ? item.LogSensorProfiles[i] : item.SensorProfiles[i],
                            item.SensorColors.Length > i ? item.SensorColors[i] : item.PaintWideStroke);
                    }
                    j++;
                }   
            }
        }

        #endregion
    }

    public class SensorVisibility : AvaloniaObject
    {
        public SensorVisibility() : base()
        {
            
        }

        AvaloniaProperty<bool> VisibleProperty = AvaloniaProperty.Register<SensorVisibility, bool>(
            nameof(Visible), true, defaultBindingMode: BindingMode.TwoWay);

        public bool Visible
        {
            get => (bool)GetValue(VisibleProperty);
            set
            {
                SetValue(VisibleProperty, value);
            }
        }
        public string Name { get => $"Sensor {Index}"; }
        public int Index { get; set; }
    }
}
