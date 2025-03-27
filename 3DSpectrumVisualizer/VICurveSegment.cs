using System;
using System.Collections.Generic;
using SkiaSharp;

namespace _3DSpectrumVisualizer
{
    public class VICurveSegment
    {
        public VICurveSegment(IEnumerable<SKPoint> points)
        {
            var rator = points.GetEnumerator();
            try
            {
                if (rator.MoveNext())
                {
                    Path.MoveTo(rator.Current);
                }
                while (rator.MoveNext())
                {
                    Path.LineTo(rator.Current);
                }
            }
            finally
            {
                rator.Dispose();
            }
        }

        public SKPath Path { get; set; } = new SKPath();

        
    }
}