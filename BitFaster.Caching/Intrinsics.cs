#if !NETSTANDARD2_0
using System.Runtime.Intrinsics.X86;
#endif

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a marker interface to enable instruction set hardware intrinsics.
    /// </summary>
    public interface IsaProbe
    {
        /// <summary>
        /// Gets a value indicating whether AVX2 is supported.
        /// </summary>
        bool IsAvx2Supported { get; }
    }

    /// <summary>
    /// Detect support for hardware instructions via intrinsics.
    /// </summary>
    public readonly struct DetectIsa : IsaProbe
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
    /// Force disable hardware instructions via intrinsics.
    /// </summary>
    public readonly struct DisableHardwareIntrinsics : IsaProbe
    {
        /// <inheritdoc/>
        public bool IsAvx2Supported => false;
    }
}
