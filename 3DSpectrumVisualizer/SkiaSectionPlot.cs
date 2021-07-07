using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Input;
using Avalonia;
using System.Linq;
using MoreLinq;

namespace _3DSpectrumVisualizer
{
    class SkiaSectionPlot : SkiaCustomControl
    {
        public SkiaSectionPlot() : base()
        {
            PointerMoved += SkiaSectionPlot_PointerMoved;
            PointerWheelChanged += SkiaSectionPlot_PointerWheelChanged;
            
        }

        #region Properties
        public SKPaint FontPaint { get; set; } = new SKPaint()
        { Color = SKColor.Parse("#ECE2E2"), StrokeWidth = 2, TextSize = 14.0f, TextScaleX = 1 };

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

        public float TimeAxisInterval { get; set; } = 5;

        public int AMURoundingDigits { get; set; } = 1;

        public bool DisableRendering { get; set; } = false;

        public float AMU { get; set; } = 1;

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

        public void Autoscale()
        {
            AutoscaleX(false);
            AutoscaleY();
        }

        public void AutoscaleX(bool invalidate = true)
        {
            XScaling = (float)Bounds.Width / DataRepositories.Max(x => x.Duration);
            XTranslate = 0;
            if (invalidate) InvalidateVisual();
        }

        public void AutoscaleY(bool invalidate = true)
        {
            float max = DataRepositories.Max(x => x.Max);
            float min = DataRepositories.Min(x => x.Min);
            if (DataRepositories.Any(x => x.LogarithmicIntensity))
            {
                max = MathF.Log10(max);
                min = MathF.Log10(min);
            }
            YScaling = (float)Bounds.Height * 0.98f / (max - min);
            YTranslate = min * YScaling;
            if (invalidate) InvalidateVisual();
        }

        #region Private

        private Point _LastPoint;

        protected override string UpdateCoordinatesString()
        {
            return FormattableString.Invariant(
                $"Tr: ({XTranslate:F1}, {YTranslate:F1}); Sc: ({XScaling:F3}, {YScaling:F3})");
        }

        protected override CustomDrawOp PrepareCustomDrawingOperation()
        {
            if (DisableRendering) return null;
            return new DrawSectionPlot(this);
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
            if (point.Properties.IsLeftButtonPressed) //Pan
            {
                XTranslate += (float)(pos.X - _LastPoint.X);
                YTranslate += (float)(pos.Y - _LastPoint.Y);
                RaiseCoordsChanged();
                InvalidateVisual();
            }
            _LastPoint = pos;
            e.Handled = true;
        }

        #endregion

        #region Render

        private class DrawSectionPlot : CustomDrawOp
        {
            float XTr;
            float YTr;
            float XSc;
            float YSc;
            float AMU;
            private SKPaint FontPaint;
            private float TimeAxisInterval;
            private float ResultsBegin;
            private float ResultsEnd;
            IEnumerable<DataRepository> Data;

            public DrawSectionPlot(SkiaSectionPlot parent) : base(parent)
            {
                XTr = parent.XTranslate;
                YTr = parent.YTranslate + (float)parent.Bounds.Height * 0.99f;
                XSc = parent.XScaling;
                YSc = -parent.YScaling;
                AMU = MathF.Round(parent.AMU, parent.AMURoundingDigits);
                Data = parent.DataRepositories;
                FontPaint = parent.FontPaint;
                TimeAxisInterval = parent.TimeAxisInterval;
                ResultsBegin = parent.HideFirstPercentOfResults;
                ResultsEnd = parent.HideLastPercentOfResults;
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                canvas.Clear(BackgroundColor);
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    RenderTimeAxis(canvas);
                }
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    RenderRegions(canvas);
                }
                canvas.Translate(XTr, YTr);
                canvas.Scale(XSc, YSc);
                foreach (var item in Data)
                {
                    canvas.DrawPath(
                        item.LogarithmicIntensity ? item.Sections[AMU].LogPath : item.Sections[AMU].LinearPath,
                        item.SectionPaint
                        );
                }
            }

            private void RenderTimeAxis(SKCanvas canvas)
            {
                int step = (int)MathF.Ceiling(FontPaint.TextSize * TimeAxisInterval); //in seconds, since default Y unit is 1S
                int ticks = (int)Math.Floor(canvas.LocalClipBounds.Width / step);
                var min = Data.Min(x => x.StartTime);
                var margin = canvas.LocalClipBounds.Height - FontPaint.TextSize * 1.1f;
                for (int i = 0; i < ticks; i++)
                {
                    var x = i * step;
                    var s = min.AddSeconds(-XTr / XSc + x / XSc).ToLongTimeString();
                    canvas.DrawText(s, x, margin, FontPaint);
                }
            }

            private void RenderRegions(SKCanvas canvas)
            {

            }

            private void RenderTemperatureProfile(SKCanvas canvas)
            {

            }
        }

        #endregion
    }
}
