#if !NETSTANDARD2_0
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace BitFaster.Caching
{
    public interface IAvx2Toggle
    {
        bool IsSupported { get; }
    }

    public struct DetectAvx2 : IAvx2Toggle
    {
#if NETSTANDARD2_0
        public bool IsSupported => false;
#else
        public bool IsSupported => Avx2.IsSupported;
#endif
    }

    public struct DisableAvx2 : IAvx2Toggle
    {
        public bool IsSupported => false;
    }
}
