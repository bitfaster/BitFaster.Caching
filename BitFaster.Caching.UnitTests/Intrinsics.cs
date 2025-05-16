#if NET
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
#endif

using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public static class Intrinsics
    {
        public static void SkipAvxIfNotSupported<I>()
        {
#if NET
            // when we are trying to test Avx2/Arm64, skip the test if it's not supported
            Skip.If(typeof(I) == typeof(DetectIsa) && !(Avx2.IsSupported || AdvSimd.Arm64.IsSupported));
#else
            Skip.If(true);
#endif
        }
    }
}


