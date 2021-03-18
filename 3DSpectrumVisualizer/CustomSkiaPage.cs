using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using System.Threading;
using System.Collections.Generic;

namespace _3DSpectrumVisualizer
{
    public class CustomSkiaPage : Control
    {
        public CustomSkiaPage()
        {
            ClipToBounds = true;
        }

        public float ScalingFactor { get; set; } = 50;
        public float XTranslate { get; set; } = 10;
        public float YTranslate { get; set; } = 10;
        public float XRotate { get; set; } = 50;
        public float YRotate { get; set; } = 50;
        public float ZRotate { get; set; } = 50;
        public float ZTranslate { get; set; } = 50;
        public float ZScalingFactor { get; set; } = 50;
        public float ScanSpacing { get; set; } = 50;
        public bool RenderEnabled { get; set; } = false;

        class CustomDrawOp : ICustomDrawOperation
        {
            private readonly SK3dView View3D;
            private float Scaling;
            private float ScanSpacing;
            private float ZScaling;
            static SKColor BackgroundColor = new SKColor(211, 215, 222);
            //static Stopwatch St = Stopwatch.StartNew();

            public CustomDrawOp(
                Rect bounds, 
                float scale, 
                float zScale,
                float xTranslate, 
                float yTranslate, 
                float zTranslate,
                float xRotate,
                float yRotate,
                float zRotate,
                float scanSpacing)
            {
                ScanSpacing = -scanSpacing;
                Bounds = bounds;
                Scaling = scale;
                ZScaling = zScale;
                View3D = new SK3dView();
                View3D.Translate(xTranslate, yTranslate, zTranslate);
                View3D.RotateXDegrees(xRotate);
                View3D.RotateYDegrees(yRotate);
                View3D.RotateZDegrees(zRotate);
            }
            
            public void Dispose()
            {
                // No-op
            }

            public Rect Bounds { get; }
            public bool HitTest(Point p) => false;
            public bool Equals(ICustomDrawOperation other) => false;
            public void Render(IDrawingContextImpl context)
            {
                bool lockTaken = Monitor.TryEnter(Program.UpdateSynchronizingObject);
                if (!lockTaken) return;

                try
                {
                    var canvas = (context as ISkiaDrawingContextImpl)?.SkCanvas;
                    using (SKAutoCanvasRestore ar1 = new SKAutoCanvasRestore(canvas))
                    {
                        canvas.Clear(BackgroundColor);
                        canvas.Scale(Scaling);
                        View3D.Save();
                        foreach (var item in Program.Repositories)
                        {
                            foreach (var scan in item.Results)
                            {
                                using (SKAutoCanvasRestore ar2 = new SKAutoCanvasRestore(canvas))
                                {
                                    View3D.TranslateY(ScanSpacing);
                                    View3D.Save();
                                    View3D.RotateXDegrees(90);
                                    View3D.ApplyToCanvas(canvas);
                                    canvas.Scale(1, ZScaling);
                                    canvas.DrawPath(scan.Path2D, item.Paint);
                                    View3D.Restore();
                                }
                            }
                        }
                        View3D.Restore();
                    }
                }
                catch (Exception)
                {

                }
                finally
                {
                    Monitor.Exit(Program.UpdateSynchronizingObject);
                }
            }    
        }


        
        public override void Render(DrawingContext context)
        {
            if (!RenderEnabled) return;
            context.Custom(new CustomDrawOp(
                new Rect(0, 0, Bounds.Width, Bounds.Height), 
                ScalingFactor / 5,
                ZScalingFactor / 50,
                XTranslate,
                -(YTranslate),
                ZTranslate / 50,
                (XRotate - 50) * 4 + 90,
                (YRotate - 50) * 4,
                (ZRotate - 50) * 4,
                ScanSpacing / 50
                ));
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        }
    }
}
