using System;
using System.Diagnostics;
using System.Linq;

#if !NETSTANDARD2_0
using System.Runtime.Intrinsics.X86;
#endif

namespace BitFaster.Caching.Buffers
{
    /// <summary>
    /// Provides a striped bounded buffer. Add operations use thread ID to index into
    /// the underlying array of buffers, and if TryAdd is contended the thread ID is 
    /// rehashed to select a different buffer to retry up to 3 times. Using this approach
    /// writes scale linearly with number of concurrent threads.
    /// </summary>
    [DebuggerDisplay("Count = {Count}/{Capacity}")]
    public sealed class StripedMpscBuffer<T> where T : class
    {
        const int MaxAttempts = 3;

        private MpscBoundedBuffer<T>[] buffers;

        public StripedMpscBuffer(int stripeCount, int bufferSize)
            : this(new StripedBufferSize(bufferSize, stripeCount))
        { 
        }

        public StripedMpscBuffer(StripedBufferSize bufferSize)
        {
            buffers = new MpscBoundedBuffer<T>[bufferSize.StripeCount];

            for (var i = 0; i < bufferSize.StripeCount; i++)
            {
                buffers[i] = new MpscBoundedBuffer<T>(bufferSize.BufferSize);
            }
        }

        public int Count => buffers.Sum(b => b.Count);

        public int Capacity => buffers.Length * buffers[0].Capacity;

        public int DrainTo(T[] outputBuffer)
        {
            var count = 0;

            for (var i = 0; i < buffers.Length; i++)
            {
                if (count == outputBuffer.Length)
                {
                    break;
                }

                var segment = new ArraySegment<T>(outputBuffer, count, outputBuffer.Length - count);
                count += buffers[i].DrainTo(segment);
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

            var z = Mix64((ulong)Environment.CurrentManagedThreadId);
            var inc = (int)(z >> 32) | 1;
            var h = (int)z;

            var mask = buffers.Length - 1;

            var result = BufferStatus.Empty;

            for (var i = 0; i < MaxAttempts; i++)
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
            for (var i = 0; i < buffers.Length; i++)
            {
                buffers[i].Clear();
            }
        }

        // Computes Stafford variant 13 of 64-bit mix function.
        // http://zimbry.blogspot.com/2011/09/better-bit-mixing-improving-on.html
        private static ulong Mix64(ulong z)
        {
            z = (z ^ z >> 30) * 0xbf58476d1ce4e5b9L;
            z = (z ^ z >> 27) * 0x94d049bb133111ebL;
            return z ^ z >> 31;
        }
    }
}
