﻿using Avalonia;
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
    public abstract class SkiaCustomControl : UserControl
    {
        private System.Timers.Timer _RedrawTimer = new System.Timers.Timer() { Enabled = false, Interval = 30, AutoReset = true };
        private Task _RedrawTask;
        private void _RedrawTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            InvalidateVisual();
        }

        public SkiaCustomControl()
        {
            ClipToBounds = true;
            _RedrawTimer.Elapsed += _RedrawTimer_Elapsed;
            this.AttachedToVisualTree += SkiaCustomControl_AttachedToVisualTree;
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
            public abstract bool Equals(ICustomDrawOperation other);
            public void Render(IDrawingContextImpl context)
            {
                var c = ((ISkiaDrawingContextImpl)context).SkCanvas;
                using (SKAutoCanvasRestore ar1 = new SKAutoCanvasRestore(c))
                {
                    RenderCanvas(c);
                }
            }
            protected abstract void RenderCanvas(SKCanvas canvas);
        }

        public override void Render(DrawingContext context)
        {
            context.Custom(PrepareCustomDrawingOperation());
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
            InvalidateVisual();
        }
    }
}
