using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace _3DSpectrumVisualizer
{
    class SkiaVIPlot : SkiaCustomControl
    {
        public static float TicksScale { get; set; } = 1;

        public SkiaVIPlot() : base()
        {
            PointerMoved += SkiaSectionPlot_PointerMoved;
            PointerWheelChanged += SkiaSectionPlot_PointerWheelChanged;
            PointerPressed += SkiaSectionPlot_PointerPressed;
        }

        #region Properties

        public SKPaint FontPaint { get; set; } = new SKPaint()
        { 
            Color = SKColor.Parse("#ECE2E2"), StrokeWidth = 1, TextSize = 14.0f, TextScaleX = 1,
            IsAntialias = true
        };

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

        public bool DisableRendering { get; set; } = false;

        public IEnumerable<DataRepositoryBase> DataRepositories { get; set; } = new List<DataRepositoryBase>();

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

            InvalidateVisual();
        }

        #endregion

        #region Private

        private Point _LastPoint;
        private Point? _LastPressedPoint;

        protected override string UpdateCoordinatesString()
        {
            return FormattableString.Invariant(
                $"Tr: ({XTranslate:F1}, {YTranslate:F1}); Sc: ({XScaling:F3}, {YScaling:F3})");
        }

        protected override CustomDrawOp PrepareCustomDrawingOperation()
        {
            if (DisableRendering) return null;
            return new DrawVIPlot(this, (float)(_LastPressedPoint?.X ?? Bounds.Width / 2), (float)(_LastPressedPoint?.Y ?? Bounds.Height / 2));
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

        #region Render

        private class DrawVIPlot : CustomDrawOp
        {
            public static float HeightReduction { get; set; } = 0.95f;

            private readonly float XTr;
            private readonly float YTr;
            private readonly float XSc;
            private readonly float YSc;
            private readonly SKPaint FontPaint;
            private readonly float ResultsBegin;
            private readonly float ResultsEnd;
            private readonly float LastMouseY;
            private readonly float LastMouseX;
            private readonly IEnumerable<DataRepositoryBase> Data;

            public DrawVIPlot(SkiaVIPlot parent, float lastMouseX, float lastMouseY) : base(parent)
            {
                XTr = parent.XTranslate;
                YTr = parent.YTranslate + (float)parent.Bounds.Height /** 0.95f*/;
                XSc = parent.XScaling;
                YSc = parent.YScaling;
                Data = parent.DataRepositories.Where(x => x.Enabled);
                FontPaint = parent.FontPaint;
                ResultsBegin = 1 - parent.HideFirstPercentOfResults;
                ResultsEnd = 1 - parent.HideLastPercentOfResults;
                LastMouseY = lastMouseY;
                LastMouseX = lastMouseX;
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                if (!Data.Any()) return;
                canvas.Clear(BackgroundColor);
                var h = canvas.LocalClipBounds.Height * HeightReduction;
                Exception lastError = null;
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    canvas.Translate(XTr, YTr);
                    canvas.Scale(XSc, -YSc);
                    foreach (var item in Data)
                    {
                        SKPath path = item.VIModeProfile;
                        canvas.DrawPath(path, item.SectionPaint);
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
                float value = (YTr - LastMouseY) / YSc;
                if (Data.Any(x => x.LogarithmicIntensity)) value = MathF.Pow(10, value);
                string text = value.ToString(IntensityLabelFormat);
                canvas.DrawText(text, 0, LastMouseY + FontPaint.TextSize * 1.1f, FontPaint);
                canvas.DrawLine(0, LastMouseY, FontPaint.MeasureText(text) * 1.5f * TicksScale, LastMouseY, FontPaint);
            }
        }

        #endregion
    }

}