using SkiaSharp;
using System;
using System.Collections.Generic;

namespace _3DSpectrumVisualizer
{
    public class SpectrumSection
    {
        public SpectrumSection()
        { }
        public SpectrumSection(float firstPoint) : this()
        {
            LinearPath.MoveTo(0, firstPoint);
            LogPath.MoveTo(0, MathF.Log10(firstPoint));
        }

        #region Properties

        public SKPath LinearPath { get; private set; } = new SKPath();

        public SKPath LogPath { get; private set; } = new SKPath();

        #endregion

        public void AddPoint(float x, float y)
        {
            LinearPath.LineTo(x, y);
            float ly = MathF.Log10(y);
            if (!float.IsNaN(ly)) LogPath.LineTo(x, ly);
        }
    }

}
