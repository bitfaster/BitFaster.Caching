
using System.Runtime.Intrinsics.X86;
using Xunit;

namespace BitFaster.Caching.UnitTests
{
    public static class Intrinsics
    {
        public static void SkipAvxIfNotSupported<I>()
        {
            // when we are trying to test Avx2, skip the test if it's not supported
            Skip.If(typeof(I) == typeof(DetectIsa) && !Avx2.IsSupported);
        }
    }
}
