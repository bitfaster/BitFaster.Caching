using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching.Buffers
{
    // Used to avoid conditional compilation to work with Span<T> and ArraySegment<T>.
    internal static class ArrayExtensions
    {
#if NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T[] WrapAsSpan<T>(this T[] array)
        { 
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ArraySegment<T> WrapAsSegment<T>(this T[] array)
        {
            return new ArraySegment<T>(array);
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Span<T> WrapAsSpan<T>(this T[] array)
        { 
            return array.AsSpan();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Span<T> WrapAsSegment<T>(this Span<T> span)
        {
            return span;
        }
#endif
    }
}
