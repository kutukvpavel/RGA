using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Input;
using Avalonia;

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

        public int AMURoundingDigits { get; set; } = 1;

        public bool DisableRendering { get; set; } = false;

        public float AMU { get; set; } = 1;

        public IEnumerable<DataRepository> DataRepositories { get; set; } = new List<DataRepository>();

        public float XTranslate { get; set; } = 0;

        public float YTranslate { get; set; } = 0;

        public float XScaling { get; set; } = 1;

        public float YScaling { get; set; } = 1;

        #endregion

        protected override string UpdateCoordinatesString()
        {
            return FormattableString.Invariant(
                $"Tr: ({XTranslate:F1}, {YTranslate:F1}); Sc: ({XScaling:F3}, {YScaling:F3})");
        }

        #region Private

        private Point _LastPoint;

        protected override CustomDrawOp PrepareCustomDrawingOperation()
        {
            if (DisableRendering) return null;
            return new DrawSectionPlot(this);
        }

        private void SkiaSectionPlot_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            //var point = e.GetCurrentPoint(this);
            //var pos = point.Position;
            var delta = (float)e.Delta.Y / 10;
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
                correction = YScaling;
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
            IEnumerable<DataRepository> Data;

            public DrawSectionPlot(SkiaSectionPlot parent) : base(parent)
            {
                XTr = parent.XTranslate;
                YTr = parent.YTranslate + (float)parent.Bounds.Height * 0.9f;
                XSc = parent.XScaling;
                YSc = -parent.YScaling;
                AMU = MathF.Round(parent.AMU, parent.AMURoundingDigits);
                Data = parent.DataRepositories;
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                canvas.Clear(BackgroundColor);
                foreach (var item in Data)
                {
                    canvas.Translate(XTr, YTr);
                    canvas.Scale(XSc, YSc);
                    canvas.DrawPath(
                        item.LogarithmicIntensity ? item.Sections[AMU].LogPath : item.Sections[AMU].LinearPath,
                        item.SectionPaint
                        );
                }
            }
        }

        #endregion
    }
}
