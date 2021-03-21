using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace _3DSpectrumVisualizer
{
    public class Skia3DSpectrum : SkiaCustomControl
    {
        public Skia3DSpectrum() : base()
        {
            PointerMoved += Skia3DSpectrum_PointerMoved;
            PointerWheelChanged += Skia3DSpectrum_PointerWheelChanged;
        }

        #region Properties

        private float _ScalingFactor = 1;
        public float ScalingFactor
        {
            get => _ScalingFactor;
            set { if (value > 0) _ScalingFactor = value; }
        }
        public float XTranslate { get; set; } = 10;
        public float YTranslate { get; set; } = 10;
        public float XRotate { get; set; } = 0;
        public float YRotate { get; set; } = 0;
        public float ZRotate { get; set; } = 0;
        public float ZTranslate { get; set; } = -10;
        private float _ZScalingFactor = 0.1f;
        public float ZScalingFactor
        {
            get => _ZScalingFactor;
            set { if (value > 0) _ZScalingFactor = value; }
        }
        private float _ScanSpacing = 1;
        public float ScanSpacing
        {
            get => _ScanSpacing;
            set { if (value > 0) _ScanSpacing = value; }
        }

        public readonly AvaloniaProperty<string> CoordinatesString =
            AvaloniaProperty.Register<Skia3DSpectrum, string>("CoordinatesString");

        #endregion

        #region Private

        private Point _LastPoint;

        private void UpdateCoordsString()
        {
            RaisePropertyChanged<string>(CoordinatesString, new Avalonia.Data.Optional<string>(),
                $"Rot: ({XRotate:F0}, {YRotate:F0}, {ZRotate:F0}); Tr: ({XTranslate:F0}, {YTranslate:F0}, {ZTranslate:F0})");
        }

        private void Skia3DSpectrum_PointerMoved(object sender, PointerEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            var pos = point.Position;
            if (point.Properties.IsLeftButtonPressed) //Pan
            {
                XTranslate += (float)(pos.X - _LastPoint.X);
                YTranslate += (float)(pos.Y - _LastPoint.Y);
            }
            else if (point.Properties.IsRightButtonPressed) //Rotate
            {
                ZRotate += (float)(pos.X - _LastPoint.X);
                XRotate += (float)(pos.Y - _LastPoint.Y);
            }
            _LastPoint = pos;
            UpdateCoordsString();
            e.Handled = true;
        }

        private void Skia3DSpectrum_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            var delta = (float)e.Delta.Y / 10;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                ZScalingFactor += delta;
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                ScanSpacing += delta / 10;
            else
                ScalingFactor += delta;
            e.Handled = true;
        }

        #endregion

        #region Render

        protected override CustomDrawOp PrepareCustomDrawingOperation()
        {
            return new Draw3DSpectrum(
                this,
                ScalingFactor,
                ZScalingFactor,
                XTranslate,
                -YTranslate,
                ZTranslate,
                -XRotate + 90,
                YRotate,
                ZRotate,
                ScanSpacing
                );
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

            public Draw3DSpectrum(
                Control parent,
                float scale,
                float zScale,
                float xTranslate,
                float yTranslate,
                float zTranslate,
                float xRotate,
                float yRotate,
                float zRotate,
                float scanSpacing) : base(parent)
            {
                ScanSpacing = -scanSpacing;
                Scaling = scale;
                ZScaling = zScale;
                XTranslate = xTranslate;
                YTranslate = yTranslate;
                View3D = new SK3dView();
                View3D.Translate(xTranslate, yTranslate, zTranslate);
                View3D.RotateXDegrees(xRotate);
                View3D.RotateYDegrees(yRotate);
                View3D.RotateZDegrees(zRotate);
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                canvas.Clear(BackgroundColor);
                //canvas.Translate(XTranslate, YTranslate);
                canvas.Scale(Scaling);
                foreach (var item in Program.Repositories)
                {
                    View3D.Save();
                    for (int i = 0; i < item.Results.Count; i++)
                    {
                        var scan = item.Results[i];
                        if (scan.Path2D == null) continue;
                        using (SKAutoCanvasRestore ar2 = new SKAutoCanvasRestore(canvas))
                        {
                            View3D.TranslateY(ScanSpacing);
                            View3D.Save();
                            View3D.RotateXDegrees(90);
                            View3D.ApplyToCanvas(canvas);
                            canvas.Scale(1, ZScaling);
                            canvas.DrawPath(scan.Path2D, item.PaintStroke);
                            View3D.Restore();
                        }
                    }
                    View3D.Restore();
                }
            }
        }

        #endregion
    }
}
