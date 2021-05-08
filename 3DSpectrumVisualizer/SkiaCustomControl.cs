using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
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
    public abstract class SkiaCustomControl : UserControl
    {
        public static IValueConverter ColorConverter = new FuncValueConverter<SKColor, Color>(
            (x) => Color.FromArgb(x.Alpha, x.Red, x.Green, x.Blue));

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

        public new SKColor Background
        {
            get => CustomDrawOp.BackgroundColor;
            set
            {
                if (CustomDrawOp.BackgroundColor == value) return;
                CustomDrawOp.BackgroundColor = value;
                RaisePropertyChanged(_BackgroundProperty, new Avalonia.Data.Optional<SKColor>(),
                    new Avalonia.Data.BindingValue<SKColor>(value));
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

        protected abstract class CustomDrawOp : ICustomDrawOperation
        {
            protected Control _Parent;

            public static SKColor BackgroundColor { get; set; } = new SKColor(211, 215, 222);

            public CustomDrawOp(Control parent)
            {
                Bounds = new Rect(0, 0, parent.Bounds.Width, parent.Bounds.Height);
                _Parent = parent;
            }

            public void Dispose()
            {
                // No-op
            }

            public Rect Bounds { get; }
            public bool HitTest(Point p)
            {
                return true;
            }
            public virtual bool Equals(ICustomDrawOperation other) => false;
            public void Render(IDrawingContextImpl context)
            {
                var c = ((ISkiaDrawingContextImpl)context).SkCanvas;
                try
                {
                    using (SKAutoCanvasRestore ar1 = new SKAutoCanvasRestore(c))
                    {
                        RenderCanvas(c);
                    }
                }
                catch (Exception ex)
                {
                    var p = new SKPaint(new SKFont(SKTypeface.Default)) { Color = SKColor.Parse("#F40A0A") };
                    c.Translate(0, p.TextSize);
                    c.DrawText(ex.ToString(), 0, 0, p);
                }
            }
            protected abstract void RenderCanvas(SKCanvas canvas);
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
