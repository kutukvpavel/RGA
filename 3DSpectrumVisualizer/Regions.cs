using SkiaSharp;
using System;
using System.Collections.Generic;

namespace _3DSpectrumVisualizer
{
    public interface IRegion
    {
        public bool HasCompleted { get; }
        public float StartTimeOffset { get; }
        public float EndTimeOffset { get; }
        public DataRepositoryBase Parent { get; }
        public static SKRect GetRect(IRegion reg, float y1, float y2)
        {
            return new SKRect(reg.StartTimeOffset, y2, reg.HasCompleted ? reg.EndTimeOffset : (float)((reg.Parent.EndTime - reg.Parent.StartTime).TotalSeconds), y1);
        }
    }

    public class UVRegion : IRegion
    {
        public UVRegion(DataRepositoryBase parent, float startTimeOffset, float endTimeOffset = -1)
        {
            Parent = parent;
            StartTimeOffset = startTimeOffset;
            HasCompleted = endTimeOffset >= 0;
            EndTimeOffset = HasCompleted ? endTimeOffset : startTimeOffset;
        }

        public DataRepositoryBase Parent { get; }
        public float StartTimeOffset { get; }
        public float EndTimeOffset { get; protected set; }
        public bool HasCompleted { get; protected set; }

        public void Complete(float endTimeOffset)
        {
            EndTimeOffset = endTimeOffset;
            HasCompleted = true;
        }
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
