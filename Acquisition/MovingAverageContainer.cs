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
                InfiniteThrowHelper(v);
                return new Accumulator(a.Buffer - v, a.Length - 1);
            }
            public static Accumulator operator +(Accumulator a, double v)
            {
                InfiniteThrowHelper(v);
                return new Accumulator(a.Buffer + v, a.Length + 1);
            }

            public Accumulator(double b, int l = 1)
            {
                if (!double.IsFinite(b)) throw new ArgumentException("Accumulator can not be initialized with a non-finite value.");
                if (l < 1) throw new ArgumentException("Accumulator length can not be less than 1.");
                Buffer = b;
                Length = l;
            }

            public double Buffer { get; }
            public int Length { get; }

            private static void InfiniteThrowHelper(double v)
            {
                if (!double.IsFinite(v)) throw new ArgumentException("Accumulator has encountered a non-finite number.");
            }
        }

        private List<Accumulator> accumulators;

        public MovingAverageContainer(int windowWidth, int capacity = 65) : base(windowWidth)
        {
            Width = windowWidth;
            accumulators = new List<Accumulator>(capacity);
        }

        public event EventHandler<ExceptionEventArgs> LogException;
        public double[] CurrentAverage 
        {
            get
            { 
                lock (SynchronizingObject)
                {
                    return accumulators.Select(x => x.Buffer / x.Length).ToArray();
                }
            }
        }
        public int Width { get; }
        public object SynchronizingObject { get; set; } = new object();

        public new void Enqueue(int[] data)
        {
            lock (SynchronizingObject)
            {
                if (Count == Width)
                {
                    int[] last = Dequeue();
                    for (int i = 0; i < last.Length; i++)
                    {
                        try
                        {
                            accumulators[i] -= last[i];
                        }
                        catch (ArgumentException ex)
                        {
                            NonFiniteNumberHelper(ex, last[i], accumulators[i].Buffer);
                        }
                    }
                }
                for (int i = 0; i < data.Length; i++)
                {
                    if (accumulators.Count > i)
                    {
                        try
                        {
                            accumulators[i] += data[i];
                        }
                        catch (ArgumentException ex)
                        {
                            NonFiniteNumberHelper(ex, data[i], accumulators[i].Buffer);
                        }
                    }
                    else
                    {
                        try
                        {
                            accumulators.Add(new Accumulator(data[i]));
                        }
                        catch (ArgumentException ex)
                        {
                            NonFiniteNumberHelper(ex, data[i], 0);
                        }
                    }
                }
                base.Enqueue(data);
            }
        }

        public void Trim(int startIndex, int count)
        {
            lock (SynchronizingObject)
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

        private void NonFiniteNumberHelper(ArgumentException ex, double nv, double av)
        {
            LogException?.Invoke(this, new ExceptionEventArgs(ex, $"New value: {nv}, accumulator value: {av}."));
        }
    }
}
