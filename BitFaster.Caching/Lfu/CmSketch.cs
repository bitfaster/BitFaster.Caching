using System.Collections.Generic;

namespace BitFaster.Caching.Lfu
{
    /// <inheritdoc/>
    public sealed class CmSketch<T> : CmSketchCore<T, DetectIsa>
    {
        /// <summary>
        /// Initializes a new instance of the CmSketch class with the specified maximum size and equality comparer.
        /// </summary>
        /// <param name="maximumSize">The maximum size.</param>
        /// <param name="comparer">The equality comparer.</param>
        public CmSketch(long maximumSize, IEqualityComparer<T> comparer) 
            : base(maximumSize, comparer)
        {
        }
    }
}
