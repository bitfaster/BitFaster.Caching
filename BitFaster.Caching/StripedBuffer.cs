using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;

namespace BitFaster.Caching
{
    public enum Status
    {
        Full,
        Empty,
        Success,
        Contended,
    }

    public class StripedBuffer<T>
    {
        const int MaxAttempts = 3;

        private BoundedBuffer<T>[] buffers;

        public StripedBuffer(int bufferSize, int concurrencyLevel)
        {
            this.buffers = new BoundedBuffer<T>[concurrencyLevel];

            for (int i = 0; i < concurrencyLevel; i++)
            {
                this.buffers[i] = new BoundedBuffer<T>(bufferSize);
            }
        }

        public int DrainTo(T[] buffer)
        {
            int count = 0;
            int bufferIndex = 0;

            do
            {
                var s = buffers[bufferIndex].TryTake(out T item);

                switch (s)
                {
                    case Status.Empty:
                        bufferIndex++;
                        break;
                    case Status.Success:
                        buffer[count++] = item;
                        break;
                    case Status.Contended:
                        // retry
                        break;
                }
            }
            while (count < buffer.Length && bufferIndex < this.buffers.Length);

            return count;
        }

        public Status TryAdd(T item)
        {
            //ulong z = Mix64((ulong)Environment.CurrentManagedThreadId);
            //int inc = (int)(z >> 32) | 1;
            //int h = (int)z;
            int mask = buffers.Length - 1;
            int h = Environment.CurrentManagedThreadId;

            Status result = Status.Empty;

            for (int i = 0; i < MaxAttempts; i++)
            {
                result = buffers[h & mask].TryAdd(item);

                if (result == Status.Success)
                {
                    break;
                }

                ulong z = Mix64((ulong)h);
                int inc = (int)(z >> 32) | 1;
                h += inc;
            }

            return result;
        }

        public void Clear()
        {
            for (int i = 0; i < this.buffers.Length; i++)
            {
                this.buffers[i].Clear();
            }
        }

        // Computes Stafford variant 13 of 64-bit mix function.
        // http://zimbry.blogspot.com/2011/09/better-bit-mixing-improving-on.html
        private static ulong Mix64(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9L;
            z = (z ^ (z >> 27)) * 0x94d049bb133111ebL;
            return z ^ (z >> 31);
        }
    }
}
