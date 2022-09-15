#if !NETSTANDARD2_0
using System.Runtime.Intrinsics.X86;
#endif

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a marker interface to enable AVX2 specific optimization.
    /// </summary>
    public interface IAvx2Toggle
    {
        /// <summary>
        /// Gets a value indicating whether Avx2 is supported
        /// </summary>
        bool IsSupported { get; }
    }

    /// <summary>
    /// Detect AVX2 support and enable if available.
    /// </summary>
    public struct DetectAvx2 : IAvx2Toggle
    {
#if NETSTANDARD2_0
        /// <inheritdoc/>
        public bool IsSupported => false;
#else
        /// <inheritdoc/>
        public bool IsSupported => Avx2.IsSupported;
#endif
    }

    /// <summary>
    /// Force disable AVX2.
    /// </summary>
    public struct DisableAvx2 : IAvx2Toggle
    {
        /// <inheritdoc/>
        public bool IsSupported => false;
    }
}
