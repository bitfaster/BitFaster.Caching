using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
#if !NETSTANDARD2_0
using System.Runtime.Intrinsics.X86;
#endif
using System.Text;
using System.Threading;
using BitFaster.Caching.Lfu;

namespace BitFaster.Caching
{
    /// <summary>
    /// Provides a striped bounded buffer. Add operations use thread ID to index into
    /// the underlying array of buffers, and if TryAdd is contended the thread ID is 
    /// rehashed to select a different buffer to retry up to 3 times. Using this approach
    /// writes scale linearly with number of concurrent threads.
    /// </summary>
    public class StripedBuffer<T>
    {
        const int MaxAttempts = 3;

        private BoundedBuffer<T>[] buffers;

        public StripedBuffer(int bufferSize, int stripeCount)
        {
            stripeCount = BitOps.CeilingPowerOfTwo(stripeCount);
            this.buffers = new BoundedBuffer<T>[stripeCount];

            for (int i = 0; i < stripeCount; i++)
            {
                this.buffers[i] = new BoundedBuffer<T>(bufferSize);
            }
        }

        public int DrainTo(T[] outputBuffer)
        {
            int count = 0;

            for (int i = 0; i < buffers.Length; i++)
            {
                BufferStatus status = BufferStatus.Full;

                while (count < outputBuffer.Length & status != BufferStatus.Empty)
                {
                    status = buffers[i].TryTake(out T item);

                    if (status == BufferStatus.Success)
                    {
                        outputBuffer[count++] = item;
                    }
                }
            }

            return count;
        }

        public BufferStatus TryAdd(T item)
        {
            // Is using Sse42.Crc32 faster?
            //#if NETSTANDARD2_0
            //            ulong z = Mix64((ulong)Environment.CurrentManagedThreadId);
            //            int inc = (int)(z >> 32) | 1;
            //            int h = (int)z;
            //#else
            //            int inc, h;

            //            // https://rigtorp.se/notes/hashing/
            //            if (Sse42.IsSupported)
            //            {
            //                h = inc = (int)Sse42.Crc32(486187739, (uint)Environment.CurrentManagedThreadId);
            //            }
            //            else
            //            {
            //                ulong z = Mix64((ulong)Environment.CurrentManagedThreadId);
            //                inc = (int)(z >> 32) | 1;
            //                h = (int)z;
            //            }
            //#endif

            ulong z = Mix64((ulong)Environment.CurrentManagedThreadId);
            int inc = (int)(z >> 32) | 1;
            int h = (int)z;

            int mask = buffers.Length - 1;

            BufferStatus result = BufferStatus.Empty;

            for (int i = 0; i < MaxAttempts; i++)
            {
                result = buffers[h & mask].TryAdd(item);

                if (result == BufferStatus.Success)
                {
                    break;
                }

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
