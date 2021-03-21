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
    public class Skia2DWaterfall : SkiaCustomControl
    {
        public Skia2DWaterfall() : base()
        {
            
        }

        public float ScanSpacing { get; set; } = 50;
        public int SelectedRepositoryIndex { get; set; } = -1;

        protected override CustomDrawOp PrepareCustomDrawingOperation()
        {
            return new Draw2DWaterfall(
                this,
                ScanSpacing / 50,
                SelectedRepositoryIndex
                );
        }

        class Draw2DWaterfall : CustomDrawOp
        {
            private float _ScanSpacing;
            private int _SelectedRepo;
            //private SK3dView _View3D;
            private static SKMatrix _PathFlattener = SKMatrix.CreateScale(1, 0);

            public Draw2DWaterfall(
                Control parent,
                float scanSpacing,
                int selectedRepo
                ) : base(parent)
            {
                _ScanSpacing = scanSpacing;
                _SelectedRepo = selectedRepo;
                //_View3D = new SK3dView();
            }

            protected override void RenderCanvas(SKCanvas canvas)
            {
                /*if (_SelectedRepo < 0) return;
                var item = Program.Repositories[_SelectedRepo];
                for (int i = 0; i < item.Results.Count; i++)
                {
                    var scan = item.Results[i].Path2D;
                    _PathFlattener.TransY = i * _ScanSpacing;
                    scan.Transform(_PathFlattener);
                    canvas.DrawPoints(SKPointMode.)
                }*/
            }
        }
    }
}
