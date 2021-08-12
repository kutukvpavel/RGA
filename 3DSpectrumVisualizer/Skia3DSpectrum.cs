using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using MoreLinq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace _3DSpectrumVisualizer
{
    public class Skia3DSpectrum : SkiaCustomControl
    {
        public static float ScalingLowerLimit { get; set; } = 0.001f;

        public Skia3DSpectrum() : base()
        {
            PointerMoved += Skia3DSpectrum_PointerMoved;
            PointerWheelChanged += Skia3DSpectrum_PointerWheelChanged;
        }

        #region Properties
        public AvaloniaProperty<float> TimeAxisIntervalProperty =
            AvaloniaProperty.Register<Skia3DSpectrum, float>("TimeAxisInterval");

        public SKPaint FontPaint { get; set; } = new SKPaint() 
        { 
            Color = SKColor.Parse("#ECE2E2"), StrokeWidth = 2, TextSize = 0.7f, TextScaleX = 1,
            IsAntialias = true
        };
        public IEnumerable<DataRepository> DataRepositories { get; set; } = new List<DataRepository>();
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
        private float _ScalingFactor = 2;
        public float ScalingFactor
        {
            get => _ScalingFactor;
            set { if (value >= ScalingLowerLimit) _ScalingFactor = value; }
        }
        public float XTranslate { get; set; } = 10;
        public float YTranslate { get; set; } = 10;
        public float XRotate { get; set; } = 15;
        public float YRotate { get; set; } = 0;
        public float ZRotate { get; set; } = 45;
        public float ZTranslate { get; set; } = 0;
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
        private float _ZScalingFactor = 0.01f;
        public float ZScalingFactor
        {
            get => _ZScalingFactor;
            set { if (value >= ScalingLowerLimit) _ZScalingFactor = value; }
        }
        private float _ScanSpacing = 0.1f;
        public float ScanSpacing
        {
            get => _ScanSpacing;
            set { if (value > 0) _ScanSpacing = value; }
        }
        public int PointDropCoef { 
            get
            {
                if (!DataRepositories.Any()) return -1;
                int c = (int)(DataRepositories.Average(x => x.AverageScanTime) * 10 * (ScalingFactor * ScanSpacing) + 0.5f);
                if (c > 10) c = -1;
                else if (c < 3) c = 3;
                return c;
            }
        }
        private float _TimeAxisInterval = 2.5f;
        public float TimeAxisInterval
        {
            get => _TimeAxisInterval;
            set
            {
                _TimeAxisInterval = value;
                SetValue(TimeAxisIntervalProperty, _TimeAxisInterval);
            }
        }

        #endregion

        #region Private

        private Point _LastPoint;

        protected override string UpdateCoordinatesString()
        {
            return FormattableString.Invariant(
                $"Rot: ({XRotate:F0}, {YRotate:F0}, {ZRotate:F0}); Tr: ({XTranslate:F0}, {YTranslate:F0}, {ZTranslate:F0}); Sc: ({ScalingFactor:F2}, {ScanSpacing:F3}, {ZScalingFactor:F3}); D.P.: {PointDropCoef}");
        }

        private void Skia3DSpectrum_PointerMoved(object sender, PointerEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            var pos = point.Position;
            bool invalidate = false;
            if (point.Properties.IsLeftButtonPressed) //Pan
            {
                XTranslate += (float)(pos.X - _LastPoint.X);
                YTranslate += (float)(pos.Y - _LastPoint.Y);
                invalidate = true;
            }
            else if (point.Properties.IsRightButtonPressed) //Rotate
            {
                var div = ScalingFactor > 1 ? (float)Math.Sqrt(ScalingFactor) : 1;
                ZRotate += (float)(pos.X - _LastPoint.X) / div;
                XRotate += (float)(pos.Y - _LastPoint.Y) / div;
                invalidate = true;
            }
            _LastPoint = pos;
            if (invalidate)
            {
                RaiseCoordsChanged();
                InvalidateVisual();
            }
            e.Handled = true;
        }

        private void Skia3DSpectrum_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            var delta = (float)e.Delta.Y / 10;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                ZScalingFactor += ZScalingFactor * delta;
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                ScanSpacing += ScanSpacing * delta;
            else
            {
                var point = e.GetCurrentPoint(this);
                var pos = point.Position.Transform(new TranslateTransform(-Bounds.Width / 2, -Bounds.Height / 2).Value);
                float correction = ScalingFactor;
                ScalingFactor += ScalingFactor * delta;
                correction = ScalingFactor / correction; //Correct translation matrix so that the pivot point of scaling is the center of the screen
                XTranslate *= correction;
                XTranslate -= (correction - 1) * (float)pos.X;
                YTranslate *= correction;
                YTranslate -= (correction - 1) * (float)pos.Y;
            }
            RaiseCoordsChanged();
            InvalidateVisual();
            e.Handled = true;
        }

        #endregion

        #region Render

        protected override CustomDrawOp PrepareCustomDrawingOperation()
        {
            return new Draw3DSpectrum(this);
        }

        class Draw3DSpectrum : CustomDrawOp
        {
            private readonly SK3dView View3D;
            private float Scaling;
            private float ScanSpacing;
            private float ZScaling;
            private float XTranslate;
            private float YTranslate;
            private float XRotate;
            private float YRotate;
            private float ZRotate;
            private int DropCoef;
            IEnumerable<DataRepository> Data;
            private SKPaint FontPaint;
            private float TimeAxisInterval;
            private float ResultsBegin;
            private float ResultsEnd;

            public Draw3DSpectrum(Skia3DSpectrum parent) : base(parent)
            {
                ScanSpacing = parent.ScanSpacing;
                Scaling = parent.ScalingFactor;
                ZScaling = parent.ZScalingFactor;
                XTranslate = parent.XTranslate + (float)Bounds.Width / 2;
                YTranslate = parent.YTranslate + (float)Bounds.Height / 2;
                XRotate = -parent.XRotate + 90;
                YRotate = parent.YRotate;
                ZRotate = parent.ZRotate;
                DropCoef = parent.PointDropCoef;
                View3D = new SK3dView();
                View3D.RotateXDegrees(XRotate);
                View3D.RotateYDegrees(YRotate);
                View3D.RotateZDegrees(ZRotate);
                Data = parent.DataRepositories;
                FontPaint = parent.FontPaint;
                TimeAxisInterval = parent.TimeAxisInterval;
                ResultsBegin = parent.HideFirstPercentOfResults;
                ResultsEnd = parent.HideLastPercentOfResults;
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                canvas.Clear(BackgroundColor);
                canvas.Translate(XTranslate, YTranslate);
                canvas.Scale(Scaling);
                //Regions

                //Axes
                if (!Data.Any()) return;
                var dataMaxLen = Data.MaxBy(x => x.Right).FirstOrDefault();
                if (dataMaxLen == null) return;
                var dataMaxDuration = Data.Max(x => x.Duration);
                var yOffset = dataMaxDuration * ScanSpacing / 2;
                View3D.Save();
                View3D.TranslateX(-dataMaxLen.MidX);
                View3D.TranslateY(yOffset);
                if (Data.Any(x => x.LogarithmicIntensity))
                    View3D.TranslateZ(-MathF.Log10(Data.Min(x => x.PositiveMin)) * ZScaling);
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    View3D.ApplyToCanvas(canvas);
                    RenderMassAxis(canvas, dataMaxLen, dataMaxDuration);
                    RenderTimeAxis(canvas, dataMaxLen);
                }
                View3D.Restore();
                //Data
                foreach (var item in Data)
                {
                    View3D.Save();
                    View3D.TranslateX(-dataMaxLen.MidX);
                    View3D.TranslateY(yOffset);
                    for (int i = 0; i < item.Results.Count; i++)
                    {
                        if (i > 0) View3D.TranslateY(-ScanSpacing * 
                            (float)(item.Results[i].CreationTime - item.Results[i - 1].CreationTime).TotalSeconds);
                        if ((float)(item.Results[i].CreationTime - item.StartTime).TotalSeconds 
                            / item.Duration < ResultsBegin) continue;
                        if ((float)(item.EndTime - item.Results[i].CreationTime).TotalSeconds
                            / item.Duration < ResultsEnd) break;
                        if (DropCoef > 1) if (i % DropCoef == 0) continue;
                        var scan = item.Results[i];
                        var path = item.LogarithmicIntensity ? scan.LogPath2D : scan.Path2D;
                        if (path == null) continue;
                        using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                        {
                            View3D.Save();
                            View3D.RotateXDegrees(90);
                            View3D.ApplyToCanvas(canvas);
                            View3D.Restore();
                            canvas.Scale(1, ZScaling);
                            canvas.DrawPath(path, item.PaintStroke);
                        }
                    }
                    View3D.Restore();
                }
            }

            private void RenderMassAxis(SKCanvas canvas, DataRepository dataMaxLen, float dataMaxDuration)
            {
                var shift = -FontPaint.TextSize * FontPaint.TextScaleX / 3;
                var marginStart = -FontPaint.TextSize * 1.1f;
                var marginEnd = -marginStart + dataMaxDuration * (1 - ResultsEnd) * ScanSpacing;
                marginStart += dataMaxDuration * ResultsBegin * ScanSpacing;
                for (int i = 0; i <= dataMaxLen.Right; i++)
                {
                    var s = i.ToString();
                    var x = MathF.FusedMultiplyAdd(shift, s.Length, i);
                    canvas.DrawText(s, x, marginStart, FontPaint);
                    canvas.DrawText(s, x, marginEnd, FontPaint);
                }
            }

            private void RenderTimeAxis(SKCanvas canvas, DataRepository dataMaxLen)
            {
                int step = (int)MathF.Ceiling(FontPaint.TextSize * TimeAxisInterval / ScanSpacing); //in seconds, since default Y unit is 1S
                float stepScaled = step * ScanSpacing;
                var min = Data.Min(x => x.StartTime);
                var max = Data.Max(x => x.EndTime);
                int ticks = (int)MathF.Floor((float)(max - min).TotalSeconds * (1 - ResultsEnd) / step);
                var marginStart = -FontPaint.TextSize * FontPaint.TextScaleX * 5;
                var marginEnd = 1 * FontPaint.TextSize * FontPaint.TextScaleX + dataMaxLen.Right;
                for (int i = 0; i < ticks; i++)
                {
                    var s = min.AddSeconds(i * step).ToLongTimeString();
                    var y = i * stepScaled;
                    canvas.DrawText(s, marginStart, y, FontPaint);
                    canvas.DrawText(s, marginEnd, y, FontPaint);
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
