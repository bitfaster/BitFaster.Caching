#if !NETSTANDARD2_0
using System.Runtime.Intrinsics.X86;
#endif

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a marker interface to enable AVX2 specific optimization.
    /// </summary>
    public interface Isa
    {
        /// <summary>
        /// Gets a value indicating whether Avx2 is supported
        /// </summary>
        bool IsAvx2Supported { get; }
    }

    /// <summary>
    /// Detect AVX2 support and enable if available.
    /// </summary>
    public struct Detect : Isa
    {
#if NETSTANDARD2_0
        /// <inheritdoc/>
        public bool IsAvx2Supported => false;
#else
        /// <inheritdoc/>
        public bool IsAvx2Supported => Avx2.IsSupported;
#endif
    }

    /// <summary>
    /// Force disable AVX2.
    /// </summary>
    public struct Disable : Isa
    {
        /// <inheritdoc/>
        public bool IsAvx2Supported => false;
    }
}
