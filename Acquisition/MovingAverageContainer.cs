using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Acquisition
{
    public class MovingAverageContainer : Queue<int[]>
    {
        private struct Accumulator
        {
            public static Accumulator operator -(Accumulator a, double v)
            {
                return new Accumulator(a.Buffer - v, a.Length - 1);
            }
            public static Accumulator operator +(Accumulator a, double v)
            {
                return new Accumulator(a.Buffer + v, a.Length + 1);
            }

            public Accumulator(double b, int l = 1)
            {
                Buffer = b;
                Length = l;
            }

            public double Buffer { get; }
            public int Length { get; }
        }

        private List<Accumulator> accumulators;
        private readonly int width;

        public MovingAverageContainer(int windowWidth, int capacity = 65) : base(windowWidth)
        {
            width = windowWidth;
            accumulators = new List<Accumulator>(capacity);
        }

        public IEnumerable<double> CurrentAverage { get => accumulators.Select(x => x.Buffer / x.Length); }
        public int Width { get => width; }

        public new void Enqueue(int[] data)
        {
            if (Count == width)
            {
                int[] last = Dequeue();
                for (int i = 0; i < last.Length; i++)
                {
                    accumulators[i] -= last[i];
                }
            }
            for (int i = 0; i < data.Length; i++)
            {
                if (accumulators.Count > i)
                {
                    accumulators[i] += data[i];
                }
                else
                {
                    accumulators.Add(new Accumulator(data[i]));
                }
            }
            base.Enqueue(data);
        }

        public void Trim(int startIndex, int count)
        {
            accumulators = accumulators.Skip(startIndex).Take(count).ToList();
            for (int i = 0; i < Count; i++)
            {
                int[] item = Dequeue();
                item = item.Skip(startIndex).Take(count).ToArray();
                base.Enqueue(item);
            }
        }
    }
}
