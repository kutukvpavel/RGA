using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace _3DSpectrumVisualizer
{
    public abstract class SkiaCustomControl : UserControl
    {
        public static IValueConverter ColorConverter { get; set; } = new FuncValueConverter<SKColor, Color>(
            (x) => Color.FromArgb(x.Alpha, x.Red, x.Green, x.Blue));
        public static SKPaint ExceptionPaint { get; set; }
            = new SKPaint(new SKFont(SKTypeface.Default)) { Color = SKColor.Parse("#F40A0A") };
        public static bool EnableCaching { get; set; }

        private System.Timers.Timer _RedrawTimer = new System.Timers.Timer() { Enabled = false, Interval = 30, AutoReset = true };
        private Task _RedrawTask;
        protected readonly AvaloniaProperty<SKColor> _BackgroundProperty =
            AvaloniaProperty.Register<SkiaCustomControl, SKColor>("Background");
        protected readonly AvaloniaProperty<string> CoordinatesString =
            AvaloniaProperty.Register<SkiaCustomControl, string>("CoordinatesString");

        private void _RedrawTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            InvalidateVisual();
        }

        public SkiaCustomControl()
        {
            ClipToBounds = true;
            _RedrawTimer.Elapsed += _RedrawTimer_Elapsed;
            AttachedToVisualTree += SkiaCustomControl_AttachedToVisualTree;
        }

        #region Properties

        public new SKColor Background
        {
            get => CustomDrawOp.BackgroundColor;
            set
            {
                if (CustomDrawOp.BackgroundColor == value) return;
                CustomDrawOp.BackgroundColor = value;
                SetValue(_BackgroundProperty, value);
            }
        }
        public object UpdateSynchronizingObject { get; set; } = new object();
        public static bool OpenGLEnabled
        {
            get => AvaloniaLocator.Current.GetService<Avalonia.OpenGL.IPlatformOpenGlInterface>() != null;
        }
        public double RedrawInterval
        {
            get => _RedrawTimer.Interval;
            set => _RedrawTimer.Interval = value;
        }
        public bool RenderContinuous
        {
            get => _RedrawTimer.Enabled;
            set => _RedrawTimer.Enabled = value;
        }

        #endregion

        protected abstract class CustomDrawOp : ICustomDrawOperation
        {
            protected Control _Parent;
            protected SKImage _Cache;

            public static SKColor BackgroundColor { get; set; } = new SKColor(211, 215, 222);

            public CustomDrawOp(Control parent)
            {
                _Parent = parent;
            }

            protected abstract void RenderCanvas(SKCanvas canvas);
            public virtual bool Equals(ICustomDrawOperation other) => false;
            public Rect Bounds { get => new Rect(0, 0, _Parent.Bounds.Width, _Parent.Bounds.Height); }

            public virtual void Dispose()
            {
                if (_Cache != null)
                {
                    _Cache.Dispose();
                    _Cache = null;
                }
            }

            public bool HitTest(Point p)
            {
                if (_Parent.TransformedBounds == null) return false;
                var pp = _Parent.GetVisualRoot().PointToScreen(p);
                var br =_Parent.PointToScreen(_Parent.TransformedBounds.Value.Clip.BottomRight);
                var tl = _Parent.PointToScreen(_Parent.TransformedBounds.Value.Clip.TopLeft);
                return (tl.X < pp.X) && (pp.X < br.X) && (tl.Y < pp.Y) && (pp.Y < br.Y);
            }

            public void Render(IDrawingContextImpl context)
            {
                var c = (ISkiaDrawingContextImpl)context;
                try
                {
                    if (_Cache?.IsValid(c.GrContext) ?? false)
                    {
                        c.SkCanvas.DrawImage(_Cache, Bounds.ToSKRect());
                    }
                    else
                    {
                        using (SKAutoCanvasRestore ar1 = new SKAutoCanvasRestore(c.SkCanvas))
                        {
                            RenderCanvas(c.SkCanvas);
                        }
                        if (EnableCaching) _Cache = c.SkSurface.Snapshot(c.SkCanvas.DeviceClipBounds); //Additional 16mS/S
                    }
                }
                catch (Exception ex)
                {
                    c.SkCanvas.DrawText(ex.ToString(), 0, c.SkCanvas.LocalClipBounds.MidY, ExceptionPaint);
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            var d = PrepareCustomDrawingOperation();
            if (d != null) context.Custom(d);
        }

        public new void InvalidateVisual()
        {
            if (_RedrawTask?.IsCompleted ?? true)
                _RedrawTask = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (Monitor.TryEnter(UpdateSynchronizingObject))
                    {
                        base.InvalidateVisual();
                        Monitor.Exit(UpdateSynchronizingObject);
                    }
                },
                DispatcherPriority.Background
                );
        }

        protected abstract CustomDrawOp PrepareCustomDrawingOperation();

        private void SkiaCustomControl_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            RaiseCoordsChanged();
            InvalidateVisual();
        }

        protected abstract string UpdateCoordinatesString();

        protected void RaiseCoordsChanged()
        {
            RaisePropertyChanged<string>(CoordinatesString, new Avalonia.Data.Optional<string>(),
                UpdateCoordinatesString());
        }
    }
}
