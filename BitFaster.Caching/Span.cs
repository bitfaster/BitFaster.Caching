using System;
using System.Runtime.CompilerServices;

namespace BitFaster.Caching
{
#if NETSTANDARD2_0
    // To avoid an additional NuGet package reference, provide a minimal implementation of Span via ArraySegment.    
    internal ref struct Span<T>
    {
        private readonly ArraySegment<T> segment;

        public Span(ref ArraySegment<T> segment)
        {
            this.segment = segment;
        }

        public int Length 
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.segment.Count; 
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref this.segment.Array[segment.Offset + index];
        }
    }

    internal static class SpanExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this ArraySegment<T> segment)
        {
            return new Span<T>(ref segment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this T[] array)
        {
            return new ArraySegment<T>(array).AsSpan();
        }
    }
#endif
}
