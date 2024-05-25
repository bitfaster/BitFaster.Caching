#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif
#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics.Arm;
#endif

using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public static class Intrinsics
    {
        public static void SkipAvxIfNotSupported<I>()
        {
#if NETCOREAPP3_1_OR_GREATER
#if NET6_0_OR_GREATER
            // when we are trying to test Avx2, skip the test if it's not supported
            Skip.If(typeof(I) == typeof(DetectIsa) && !(Avx2.IsSupported || AdvSimd.Arm64.IsSupported));
#else
// when we are trying to test Avx2, skip the test if it's not supported
            Skip.If(typeof(I) == typeof(DetectIsa) && !Avx2.IsSupported);
#endif


#else
            Skip.If(true);
#endif
        }
    }
}


