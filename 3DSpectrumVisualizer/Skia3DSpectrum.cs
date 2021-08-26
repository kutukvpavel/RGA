using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using MoreLinq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace _3DSpectrumVisualizer
{
    public class Skia3DSpectrum : SkiaCustomControl
    {
        public static float ScalingLowerLimit { get; set; }
        public static float FastModeDepth { get; set; }
        public static float AxisLabelRotateThreshold { get; set; } = 15;

        public Skia3DSpectrum() : base()
        {
            PointerMoved += Skia3DSpectrum_PointerMoved;
            PointerWheelChanged += Skia3DSpectrum_PointerWheelChanged;
            FastModeProperty.Changed.Subscribe((e) => 
            {
                if (!IsInitialized || !e.IsEffectiveValueChange || !e.NewValue.HasValue) return;
                FastMode = e.NewValue.Value; 
                InvalidateVisual();
                RaiseCoordsChanged();
            });
        }

        #region Properties

        public AvaloniaProperty<float> TimeAxisIntervalProperty =
            AvaloniaProperty.Register<Skia3DSpectrum, float>("TimeAxisInterval");
        public AvaloniaProperty<bool> FastModeProperty = AvaloniaProperty.Register<Skia3DSpectrum, bool>("FastMode");

        public SKPaint FontPaint { get; set; } = new SKPaint() 
        { 
            Color = SKColor.Parse("#ECE2E2"), StrokeWidth = 2, TextSize = 0.7f, TextScaleX = 1,
            IsAntialias = false
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
        private bool _FastMode = false;
        public bool FastMode
        {
            get => _FastMode;
            set
            {
                _FastMode = value;
                FontPaint.IsAntialias = !_FastMode;
                SetValue(FastModeProperty, _FastMode);
            }
        }
        public int PointDropCoef { 
            get
            {
                if (!DataRepositories.Any()) return -1;
                int c = (int)(DataRepositories.Average(x => x.AverageScanTime) * 10 * (ScalingFactor * ScanSpacing) + 
                    (FastMode ? FastModeDepth : 0.5f));
                if (c > 10) c = -1;
                else if (c < 3) c -= 4;
                if (c % 2 == 0) c -= 1;
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
            private readonly float Scaling;
            private readonly float ScanSpacing;
            private readonly float ZScaling;
            private readonly float XTranslate;
            private readonly float YTranslate;
            private readonly float XRotate;
            private readonly float YRotate;
            private readonly float ZRotate;
            private readonly int DropCoef;
            readonly IEnumerable<DataRepository> Data;
            private readonly SKPaint FontPaint;
            private readonly float TimeAxisInterval;
            private readonly float ResultsBegin;
            private readonly float ResultsEnd;

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

            public override void Dispose()
            {
                View3D.Dispose();
                base.Dispose();
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                View3D.Save();
                canvas.Clear(BackgroundColor);
                canvas.Translate(XTranslate, YTranslate);
                canvas.Scale(Scaling);
                if (!Data.Any()) return;
                var dataMaxLen = Data.MaxBy(x => x.Right).FirstOrDefault();
                if (dataMaxLen == null) return;
                View3D.TranslateX(-dataMaxLen.MidX);
                View3D.Save();
                bool reverseDrawingOrder;
                {
                    float angle = MathF.Abs(ZRotate % 360);
                    reverseDrawingOrder = angle > 90 && angle < 270;
                }
                var dataMaxDuration = Data.Max(x => x.Duration);
                var yOffset = dataMaxDuration * ScanSpacing / 2;
                //Regions

                //Data
                View3D.TranslateY(reverseDrawingOrder ? -yOffset : yOffset);
                foreach (var item in Data)
                {
                    View3D.Save();
                    ScanResult lastScan = null;
                    if (reverseDrawingOrder)
                    {
                        for (int i = item.Results.Count - 1; i >= 0; i--)
                        {
                            using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                            {
                                if (RenderScan(item, i, ref lastScan, canvas, reverseDrawingOrder)) break;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < item.Results.Count; i++)
                        {
                            using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                            {
                                if (RenderScan(item, i, ref lastScan, canvas, reverseDrawingOrder)) break;
                            }
                        }
                    }
                    View3D.Restore();
                }
                View3D.Restore();
                //Axes
                bool rotateMassAxis = MathF.Abs((XRotate - 90) % 180) < AxisLabelRotateThreshold;     
                View3D.TranslateY(yOffset);
                if (Data.Any(x => x.LogarithmicIntensity))
                    View3D.TranslateZ(-MathF.Log10(Data.Min(x => x.PositiveMin)) * ZScaling);
                using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                {
                    View3D.ApplyToCanvas(canvas);
                    RenderTimeAxis(canvas, dataMaxLen);
                    if (!rotateMassAxis) RenderMassAxis(canvas, dataMaxLen, dataMaxDuration, true);
                }
                if (rotateMassAxis)
                {
                    if (!reverseDrawingOrder) View3D.TranslateY(dataMaxDuration * (ResultsEnd - 1) * ScanSpacing);
                    View3D.RotateXDegrees(-90);
                    using (SKAutoCanvasRestore ar = new SKAutoCanvasRestore(canvas))
                    {
                        View3D.ApplyToCanvas(canvas);
                        RenderMassAxis(canvas, dataMaxLen, dataMaxDuration, false);
                    }
                }
                View3D.Restore();
            }

            private bool RenderScan(DataRepository item, int i, ref ScanResult lastScan, 
                SKCanvas canvas, bool reverseOrder)
            {
                var scan = item.Results[i];
                if (lastScan != null) View3D.TranslateY(ScanSpacing *
                    (float)(lastScan.CreationTime - scan.CreationTime).TotalSeconds);
                lastScan = scan;
                if (DropCoef > 1)
                {
                    if (i % DropCoef == 0) return false;
                }
                else if (DropCoef < -1)
                {
                    if (i % DropCoef != 0) return false;
                }
                if ((float)(scan.CreationTime - item.StartTime).TotalSeconds
                    / item.Duration < ResultsBegin) return reverseOrder;
                if ((float)(item.EndTime - scan.CreationTime).TotalSeconds
                    / item.Duration < ResultsEnd) return !reverseOrder;
                var path = item.LogarithmicIntensity ? scan.LogPath2D : scan.Path2D;
                if (path == null) return false;
                View3D.Save();
                View3D.RotateXDegrees(90);
                View3D.ApplyToCanvas(canvas);
                View3D.Restore();
                canvas.Scale(1, ZScaling);
                canvas.DrawPath(path, item.PaintStroke);
                return false;
            }

            private void RenderMassAxis(SKCanvas canvas, DataRepository dataMaxLen, float dataMaxDuration, bool dual)
            {
                var shift = -FontPaint.TextSize * FontPaint.TextScaleX / 3;
                var marginStart = -FontPaint.TextSize * 1.1f;
                var marginEnd = dataMaxDuration * (1 - ResultsEnd) * ScanSpacing - marginStart;
                marginStart += dataMaxDuration * ResultsBegin * ScanSpacing;
                if (!dual) marginStart = (XRotate % 180) > 90 ? marginStart : -marginStart;
                for (int i = 0; i <= dataMaxLen.Right; i++)
                {
                    var s = i.ToString();
                    var x = MathF.FusedMultiplyAdd(shift, s.Length, i);
                    canvas.DrawText(s, x, marginStart, FontPaint);
                    if (dual) canvas.DrawText(s, x, marginEnd, FontPaint);
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
                var marginEnd = MathF.FusedMultiplyAdd(FontPaint.TextSize, FontPaint.TextScaleX, dataMaxLen.Right);
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
