using Avalonia;
using Avalonia.Input;
using Avalonia.Data;
using SkiaSharp;
using System;
using System.Collections.Generic;
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

        public IEnumerable<DataRepository> DataRepositories { get; set; } = new List<DataRepository>();

        public float XTranslate { get; set; } = 0;

        public float YTranslate { get; set; } = 0;

        public float XScaling { get; set; } = 1;

        public float YScaling { get; set; } = 1;

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
            AutoscalingYEngine(min, max, invalidate);
        }

        public void AutoscaleYForAllSections(bool invalidate = true)
        {
            if (!DataRepositories.Any()) return;
            float max = DataRepositories.Max(x => x.Max);
            float min = DataRepositories.Min(x => x.Min);
            AutoscalingYEngine(min, max, invalidate);
        }

        #endregion

        #region Private

        private Point _LastPoint;
        private Point? _LastPressedPoint;

        private void AutoscalingYEngine(float min, float max, bool invalidate)
        {
            YScaling = (float)Bounds.Height * 0.9f / (max - min);
            YTranslate = min * YScaling - (float)Bounds.Height * 0.05f;
            if (invalidate) InvalidateVisual();
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
            var delta = (float)e.Delta.Y / 10;
            float correction;
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                correction = YScaling;
                YScaling += YScaling * delta;
                correction = YScaling / correction;
                YTranslate *= correction;
                YTranslate += (correction - 1) * (float)(Bounds.Height - pos.Y);
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
                YTranslate += (float)(pos.Y - _LastPoint.Y);
                RaiseCoordsChanged();
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
            private readonly float XTr;
            private readonly float YTr;
            private readonly float XSc;
            private readonly float YSc;
            private readonly float AMU;
            private readonly SKPaint FontPaint;
            private readonly float TimeAxisInterval;
            private readonly float ResultsBegin;
            private readonly float ResultsEnd;
            private readonly bool ShowGasRegions;
            private readonly bool ShowTemperatureProfile;
            private readonly float LastMouseY;
            private readonly IEnumerable<DataRepository> Data;

            public DrawSectionPlot(SkiaSectionPlot parent, float lastMouseY) : base(parent)
            {
                XTr = parent.XTranslate;
                YTr = parent.YTranslate + (float)parent.Bounds.Height /** 0.95f*/;
                XSc = parent.XScaling;
                YSc = parent.YScaling;
                AMU = MathF.Round(parent.AMU, parent.AMURoundingDigits);
                Data = parent.DataRepositories;
                FontPaint = parent.FontPaint;
                TimeAxisInterval = parent.TimeAxisInterval;
                ResultsBegin = 1 - parent.HideFirstPercentOfResults;
                ResultsEnd = 1 - parent.HideLastPercentOfResults;
                ShowGasRegions = parent.RenderGasRegions;
                ShowTemperatureProfile = parent.RenderTemperatureProfile;
                LastMouseY = lastMouseY;
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                canvas.Clear(BackgroundColor);
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    canvas.Translate(XTr, 0);
                    canvas.Scale(XSc, 1);
                    RenderRegions(canvas);
                    if (ShowTemperatureProfile)
                    {
                        var h = canvas.LocalClipBounds.Height * 0.95f;
                        var s = h / Data.Max(x => x.TemperatureProfile.Bounds.Height) * 0.95f;
                        canvas.Translate(0, h + Data.Min(x => x.TemperatureProfile.Bounds.Top) * s);
                        canvas.Scale(1, -s);
                        RenderTemperatureProfile(canvas);
                    }
                }
                RenderTimeAxis(canvas);
                RenderIntensityAxis(canvas);
                canvas.Translate(XTr, YTr);
                canvas.Scale(XSc, -YSc);
                foreach (var item in Data)
                {
                    var path = item.LogarithmicIntensity ? item.Sections[AMU].LogPath : item.Sections[AMU].LinearPath;
                    if (ResultsEnd != 1 || ResultsBegin != 1)
                    {
                        var virtualBoundsRect = new SKRect(
                            path.Bounds.Right - path.Bounds.Width * ResultsBegin,
                            path.Bounds.Top,
                            path.Bounds.Left + path.Bounds.Width * ResultsEnd,
                            path.Bounds.Bottom);
                        canvas.ClipRect(virtualBoundsRect);
                    }
                    canvas.DrawPath(path, item.SectionPaint);
                }
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
                float value = (YTr - LastMouseY) / YSc;
                string text = value.ToString(IntensityLabelFormat);
                canvas.DrawText(text, 0, LastMouseY + FontPaint.TextSize * 1.1f, FontPaint);
                canvas.DrawLine(0, LastMouseY, FontPaint.MeasureText(text) * 1.5f * TicksScale, LastMouseY, FontPaint);
            }

            private void RenderRegions(SKCanvas canvas)
            {
                foreach (var item in Data)
                {
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
                }
            }

            private void RenderTemperatureProfile(SKCanvas canvas)
            {
                foreach (var item in Data)
                {
                    canvas.DrawPath(item.TemperatureProfile, item.TemperaturePaint);
                }
            }
        }

        #endregion
    }
}
