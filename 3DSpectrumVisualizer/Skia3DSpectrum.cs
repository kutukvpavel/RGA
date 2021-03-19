using Avalonia;
using Avalonia.Controls;
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
            
        }

        public float ScalingFactor { get; set; } = 50;
        public float XTranslate { get; set; } = 10;
        public float YTranslate { get; set; } = 10;
        public float XRotate { get; set; } = 50;
        public float YRotate { get; set; } = 50;
        public float ZRotate { get; set; } = 50;
        public float ZTranslate { get; set; } = 50;
        public float ZScalingFactor { get; set; } = 10;
        public float ScanSpacing { get; set; } = 50;


        protected override CustomDrawOp PrepareCustomDrawingOperation()
        {
            return new Draw3DSpectrum(
                new Rect(0, 0, Bounds.Width, Bounds.Height),
                ScalingFactor / 5,
                ZScalingFactor / 100,
                XTranslate,
                -(YTranslate),
                ZTranslate / 50,
                (XRotate - 50) * 2 + 90,
                (YRotate - 50) * 2,
                (ZRotate - 50) * 2,
                ScanSpacing / 50
                );
        }

        class Draw3DSpectrum : CustomDrawOp
        {
            public static SKColor BackgroundColor { get; set; } = new SKColor(211, 215, 222);

            private readonly SK3dView View3D;
            private float Scaling;
            private float ScanSpacing;
            private float ZScaling;

            public Draw3DSpectrum(
                Rect bounds, 
                float scale, 
                float zScale,
                float xTranslate, 
                float yTranslate, 
                float zTranslate,
                float xRotate,
                float yRotate,
                float zRotate,
                float scanSpacing) : base(bounds)
            {
                ScanSpacing = -scanSpacing;
                Scaling = scale;
                ZScaling = zScale;
                View3D = new SK3dView();
                View3D.Translate(xTranslate, yTranslate, zTranslate);
                View3D.RotateXDegrees(xRotate);
                View3D.RotateYDegrees(yRotate);
                View3D.RotateZDegrees(zRotate);
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                canvas.Clear(BackgroundColor);
                canvas.Scale(Scaling);
                foreach (var item in Program.Repositories)
                {
                    View3D.Save();
                    for (int i = 0; i < item.Results.Count; i++)
                    {
                        var scan = item.Results[i];
                        using (SKAutoCanvasRestore ar2 = new SKAutoCanvasRestore(canvas))
                        {
                            View3D.TranslateY(ScanSpacing);
                            View3D.Save();
                            View3D.RotateXDegrees(90);
                            View3D.ApplyToCanvas(canvas);
                            canvas.Scale(1, ZScaling);
                            canvas.DrawPath(scan.Path2D, item.PaintFill);
                            View3D.Restore();
                        }
                    }
                    View3D.Restore();
                }
            }
        }
    }
}
