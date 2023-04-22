#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public static class Intrinsics
    {
        public static void SkipAvxIfNotSupported<I>()
        {
#if NETCOREAPP3_1_OR_GREATER
            // when we are trying to test Avx2, skip the test if it's not supported
            Skip.If(typeof(I) == typeof(DetectIsa) && !Avx2.IsSupported);
#else
            Skip.If(true);
#endif
        }
    }
}


