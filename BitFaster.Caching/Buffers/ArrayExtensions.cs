using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Buffers
{
    // Used to avoid conditional compilation to work with Span<T> and ArraySegment<T>.
    internal static class ArrayExtensions
    {
#if NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T[] AsSpanOrArray<T>(this T[] array)
        {
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ArraySegment<T> AsSpanOrSegment<T>(this T[] array)
        {
            return new ArraySegment<T>(array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ArraySegment<T> Slice<T>(this T[] array, int start, int length)
        {
            return new ArraySegment<T>(array, start, length);
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Span<T> AsSpanOrArray<T>(this T[] array)
        { 
            return array.AsSpan();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Span<T> AsSpanOrSegment<T>(this Span<T> span)
        {
            return span;
        }
#endif
    }
}
