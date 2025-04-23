using Avalonia;
using Avalonia.Input;
using SkiaSharp;
using System;
using System.Collections.Generic;
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
            Color = SKColor.Parse("#0B0A0A"), StrokeWidth = 1, TextSize = 14.0f, TextScaleX = 1,
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

        public float VoltageAxisInterval { get; set; } = 0.25f;

        public string CurrentLabelFormat { get; set; } = "E2";

        public string VoltageLabelFormat { get; set; } = "F2";

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

        public void Autoscale(bool invalidate = true)
        {
            var nonEmpty = DataRepositories.Where(x => !x.VIModeProfile.IsEmpty);
            if (!nonEmpty.Any()) return;
            float yMin = nonEmpty.Min(x => x.VIModeProfile.Bounds.Top);
            float yMax = nonEmpty.Min(x => x.VIModeProfile.Bounds.Bottom);
            float xMin = nonEmpty.Min(x => x.VIModeProfile.Bounds.Left);
            float xMax = nonEmpty.Max(x => x.VIModeProfile.Bounds.Right);
            YScaling = (float)Bounds.Height * 0.9f / (yMax - yMin);
            YTranslate = 0; //yMin * YScaling - (float)Bounds.Height * 0.05f;
            XScaling = (float)Bounds.Width * 0.9f / (xMax - xMin);
            XTranslate = 0;//xMin * XScaling - (float)Bounds.Width * 0.05f;
            if (invalidate) InvalidateVisual();
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
            return new DrawVIPlot(this, (float)(_LastPressedPoint?.Y ?? Bounds.Height / 2));
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
            }
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                correction = XScaling;
                XScaling += XScaling * delta;
                correction = XScaling / correction;
                XTranslate *= correction;
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

        private class DrawVIPlot : CustomDrawOp
        {
            public static float SizeReduction { get; set; } = 0.95f;

            private readonly float XTr;
            private readonly float YTr;
            private readonly float XSc;
            private readonly float YSc;
            private readonly SKPaint FontPaint;
            private readonly float LastMouseY;
            private readonly IEnumerable<DataRepositoryBase> Data;
            private readonly float VoltageAxisInterval;
            private readonly string CurrentLabelFormat;
            private readonly string VoltageLabelFormat;

            public DrawVIPlot(SkiaVIPlot parent, float lastMouseY) : base(parent)
            {
                XTr = parent.XTranslate + (float)parent.Bounds.Width / 2;
                YTr = parent.YTranslate + (float)parent.Bounds.Height / 2;
                XSc = parent.XScaling;
                YSc = parent.YScaling;
                Data = parent.DataRepositories.Where(x => x.Enabled);
                FontPaint = parent.FontPaint;
                LastMouseY = lastMouseY;
                VoltageAxisInterval = parent.VoltageAxisInterval;
                CurrentLabelFormat = parent.CurrentLabelFormat;
                VoltageLabelFormat = parent.VoltageLabelFormat;
                foreach (var item in Data)
                {
                    item.VIPaint.PathEffect = SKPathEffect.CreateTrim(
                        (parent.HideFirstPercentOfResults * item.Duration - item.VIModeTimestamps.FirstOrDefault()) / item.VIModeDuration,
                        1 - ((parent.HideLastPercentOfResults - 1) * item.Duration + item.VIModeTimestamps.LastOrDefault()) / item.VIModeDuration);
                }
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                if (!Data.Any()) return;
                canvas.Clear(BackgroundColor);
                Exception lastError = null;
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    canvas.Translate(XTr, YTr);
                    canvas.Scale(XSc, -YSc);
                    foreach (var item in Data)
                    {
                        canvas.DrawPath(item.VIModeProfile, item.VIPaint);
                    }
                }
                RenderVoltageAxis(canvas);
                RenderCurrentAxis(canvas);
                if (lastError != null) throw lastError;
            }

            private void RenderVoltageAxis(SKCanvas canvas)
            {
                SKPoint origin = new SKPoint(XTr, YTr);
                var min = Data.Min(x =>
                {
                    if (x.VIModeProfile.GetBounds(out SKRect bounds))
                    {
                        return bounds.Left;
                    }
                    else
                    {
                        return 0;
                    }
                });
                var max = Data.Max(x =>
                {
                    if (x.VIModeProfile.GetBounds(out SKRect bounds))
                    {
                        return bounds.Right;
                    }
                    else
                    {
                        return 0;
                    }
                });
                int ticksPerSide = (int)MathF.Floor((canvas.LocalClipBounds.Width / XSc) / (2 * VoltageAxisInterval));
                if (ticksPerSide > 10) ticksPerSide = 10;
                float tripleStroke = FontPaint.StrokeWidth * 3;
                float tickHeightHalf = FontPaint.TextSize * 0.75f;
                float step = VoltageAxisInterval * XSc;
                for (int i = -ticksPerSide; i <= ticksPerSide; i++)
                {
                    var x = MathF.FusedMultiplyAdd(i, step, FontPaint.StrokeWidth + origin.X);
                    canvas.DrawText(i == 0 ? "0" : (VoltageAxisInterval * i).ToString(VoltageLabelFormat), x + tripleStroke, origin.Y + FontPaint.TextSize, FontPaint);
                    canvas.DrawLine(x, origin.Y - tickHeightHalf, x, origin.Y + tickHeightHalf, FontPaint);
                }
            }

            private void RenderCurrentAxis(SKCanvas canvas)
            {
                float value = (YTr - LastMouseY) / YSc;
                string text = value.ToString(CurrentLabelFormat);
                canvas.DrawText(text, 0, LastMouseY + FontPaint.TextSize * 1.1f, FontPaint);
                canvas.DrawLine(0, LastMouseY, FontPaint.MeasureText(text) * TicksScale, LastMouseY, FontPaint);
            }
        }

        #endregion
    }
}