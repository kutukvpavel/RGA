using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Acquisition
{
    public class MovingAverageContainer : Queue<IList<double>>
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

        public MovingAverageContainer(int windowWidth, int capacity = 650) : base(windowWidth)
        {
            Width = windowWidth;
            if (Width < 2) throw new ArgumentOutOfRangeException("Moving average window width has to be at least 2");
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
        public int Length { get => accumulators.Count; }

        public new void Enqueue(IList<double> data)
        {
            lock (SynchronizingObject)
            {
                if (Count == Width)
                {
                    var last = Dequeue();
                    for (int i = 0; i < last.Count; i++)
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
                for (int i = 0; i < data.Count; i++)
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
                    var item = Dequeue();
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
