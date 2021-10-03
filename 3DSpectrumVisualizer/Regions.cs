using SkiaSharp;
using System;
using System.Collections.Generic;

namespace _3DSpectrumVisualizer
{
    public interface IRegion
    {
        public float StartTimeOffset { get; }
        public float EndTimeOffset { get; }
        public static SKRect GetRect(IRegion reg, float y1, float y2)
        {
            return new SKRect(reg.StartTimeOffset, y2, reg.EndTimeOffset, y1);
        }
    }

    public class UVRegion : IRegion
    {
        public UVRegion(DataRepositoryBase parent, float startTimeOffset, float endTimeOffset = -1)
        {
            Parent = parent;
            StartTimeOffset = startTimeOffset;
            EndTimeOffset = endTimeOffset < 0 ? startTimeOffset : endTimeOffset;
        }

        public float StartTimeOffset { get; }
        public float EndTimeOffset { get; set; }
        public DataRepositoryBase Parent { get; }
    }

    public class GasRegion : UVRegion
    {
        public GasRegion(DataRepositoryBase parent, float timeOffset, string name)
            : this(parent, timeOffset, name, DataRepositoryBase.RegionPaintTemplate)
        {

        }
        public GasRegion(DataRepositoryBase parent, float timeOffset, string name, SKPaint paint)
            : base(parent, timeOffset)
        {
            Name = name;
            Paint = paint;
        }

        public string Name { get; }
        public SKPaint Paint { get; }
    }
}
