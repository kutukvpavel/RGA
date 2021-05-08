using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MoreLinq;

namespace _3DSpectrumVisualizer
{
    public class Skia3DSpectrum : SkiaCustomControl
    {
        public static float ScalingLowerLimit { get; set; } = 0.001f;

        public static IValueConverter ColorConverter = new FuncValueConverter<SKColor, Color>(
            (x) => Color.FromArgb(x.Alpha, x.Red, x.Green, x.Blue));

        public Skia3DSpectrum() : base()
        {
            PointerMoved += Skia3DSpectrum_PointerMoved;
            PointerWheelChanged += Skia3DSpectrum_PointerWheelChanged;
        }

        #region Properties
        public SKPaint FontPaint { get; set; } = new SKPaint() 
        { Color = SKColor.Parse("#ECE2E2"), StrokeWidth = 2, TextSize = 0.5f, TextScaleX = 1 };

        private IEnumerable<DataRepository> _Repos = new List<DataRepository>();
        public IEnumerable<DataRepository> DataRepositories
        {
            get { return _Repos; }
            set
            {
                _Repos = value;
            }
        }
        public new SKColor Background
        {
            get => Draw3DSpectrum.BackgroundColor;
            set {
                if (Draw3DSpectrum.BackgroundColor == value) return;
                Draw3DSpectrum.BackgroundColor = value;
                var grayscale = value.Red * 0.299 + value.Green * 0.587 + value.Blue * 0.114;
                FontPaint.Color = SKColor.Parse(grayscale > 167 ? "#000000" : "#FFFFFF");
                RaisePropertyChanged(_BackgroundProperty, new Avalonia.Data.Optional<SKColor>(),
                    new Avalonia.Data.BindingValue<SKColor>(value));
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
                int c = (int)(10 * (ScalingFactor * ScanSpacing) + 0.5f);
                if (c > 10) c = -1;
                else if (c < 2) c = 2;
                return c;
            }
        }
        public float TimeAxisInterval { get; set; } = 10;

        /*private readonly AvaloniaProperty<float> GeneralOpacityProperty =
            AvaloniaProperty.Register<Skia3DSpectrum, float>("GeneralOpacity");*/
        private readonly AvaloniaProperty<string> CoordinatesString =
            AvaloniaProperty.Register<Skia3DSpectrum, string>("CoordinatesString");
        private readonly AvaloniaProperty<SKColor> _BackgroundProperty =
            AvaloniaProperty.Register<Skia3DSpectrum, SKColor>("Background");

        #endregion

        #region Private

        private Point _LastPoint;

        private void UpdateCoordsString()
        {
            RaisePropertyChanged<string>(CoordinatesString, new Avalonia.Data.Optional<string>(),
                FormattableString.Invariant(
                $"Rot: ({XRotate:F0}, {YRotate:F0}, {ZRotate:F0}); Tr: ({XTranslate:F0}, {YTranslate:F0}, {ZTranslate:F0}); Sc: ({ScalingFactor:F2}, {ScanSpacing:F2}, {ZScalingFactor:F3}); D.P.: {PointDropCoef}"));
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
                InvalidateVisual();
                UpdateCoordsString();
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
                ScalingFactor += ScalingFactor * delta;
            UpdateCoordsString();
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
            public static SKColor BackgroundColor { get; set; } = new SKColor(211, 215, 222);

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
            }

            public override bool Equals(ICustomDrawOperation other)
            {
                var o = (other as Draw3DSpectrum);
                if (o == null) return false;
                return (Scaling == o.Scaling) && (ZScaling == o.ZScaling) && (ScanSpacing == o.ScanSpacing) &&
                    /*(Bounds == o.Bounds) &&*/ (XTranslate == o.XTranslate) && (YTranslate == o.YTranslate) &&
                    (XRotate == o.XRotate) && (YRotate == o.YRotate) && (ZRotate == o.ZRotate);
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                canvas.Clear(BackgroundColor);
                canvas.Translate(XTranslate, YTranslate);
                canvas.Scale(Scaling);
                //Axes
                var dataMaxLen = Data.MaxBy(x => x.Right).First();
                var dataMaxDuration = Data.Max(x => x.Duration);
                View3D.Save();
                View3D.TranslateX(-dataMaxLen.MidX); //
                View3D.TranslateY(dataMaxDuration * ScanSpacing / 2);
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
                    View3D.TranslateX(-item.MidX);
                    View3D.TranslateY(item.Duration * ScanSpacing / 2);
                    for (int i = 0; i < item.Results.Count; i++)
                    {
                        if (i > 0) View3D.TranslateY(-ScanSpacing * 
                            (float)(item.Results[i].CreationTime - item.Results[i - 1].CreationTime).TotalSeconds);
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
                var axisLabels = string.Join(' ', Enumerable.Range(0, (int)MathF.Ceiling(dataMaxLen.Right)));
                var shift = FontPaint.TextSize * FontPaint.TextScaleX / 3;
                var marginStart = -FontPaint.TextSize * 1.1f;
                var marginEnd = -marginStart + dataMaxDuration * ScanSpacing;
                for (int i = 0; i < dataMaxLen.Right; i++)
                {
                    var s = i.ToString();
                    var x = i - shift * s.Length;
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
                int ticks = (int)MathF.Floor((float)(max - min).TotalSeconds * ScanSpacing / step);
                var marginStart = -FontPaint.TextSize * FontPaint.TextScaleX * 10;
                var marginEnd = 1 * FontPaint.TextSize * FontPaint.TextScaleX + dataMaxLen.Right;
                for (int i = 0; i < ticks; i++)
                {
                    var s = min.AddSeconds(i * step).ToLongTimeString();
                    var y = i * stepScaled;
                    canvas.DrawText(s, marginStart, y, FontPaint);
                    canvas.DrawText(s, marginEnd, y, FontPaint);
                }
            }
        }

        #endregion
    }
}
