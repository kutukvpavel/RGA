using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Acquisition
{
    public class MovingAverageContainer : Queue<int[]>
    {
        private List<double> buffer;
        private int[] length;
        private readonly int width;

        public MovingAverageContainer(int capacity) : base(capacity)
        {
            width = capacity;
        }

        public IEnumerable<double> CurrentAverage { get => buffer.Select((x, i) => x / length[i]); }

        public new void Enqueue(int[] data)
        {
            if (Count == width)
            {
                int[] last = Dequeue();
                for (int i = 0; i < last.Length; i++)
                {
                    buffer[i] -= last[i];
                    length[i]--;
                }
            }
            for (int i = 0; i < data.Length; i++)
            {
                if (buffer.Count > i)
                {
                    buffer[i] += data[i];
                }
                else
                {
                    buffer.Add(data[i]);
                }
                length[i]++;
            }
            base.Enqueue(data);
        }

        public void Trim(int startIndex, int count)
        {
            buffer = buffer.Skip(startIndex).Take(count).ToList();
            length = length.Skip(startIndex).Take(count).ToArray();
            for (int i = 0; i < Count; i++)
            {
                int[] item = Dequeue();
                item = item.Skip(startIndex).Take(count).ToArray();
                base.Enqueue(item);
            }
        }
    }
}
