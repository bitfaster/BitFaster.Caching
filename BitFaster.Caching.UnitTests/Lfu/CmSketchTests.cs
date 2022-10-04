
using BitFaster.Caching.Lfu;
using FluentAssertions;
using System.Collections.Generic;


#if !NET48
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif


using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    // Test with AVX2 if it is supported
    public class CMSketchAvx2Tests : CmSketchTestBase<DetectIsa>
    {
    }

    // Test with AVX2 disabled
    public class CmSketchTests : CmSketchTestBase<DisableHardwareIntrinsics>
    {
    }

    public abstract class CmSketchTestBase<I> where I : struct, IsaProbe
    {
        private CmSketch<int, I> sketch = new CmSketch<int, I>(512, EqualityComparer<int>.Default);

        public CmSketchTestBase()
        {
            SkipAvxIfNotSupported();
        }

        [SkippableFact]
        public void WhenCapacityIsZeroDefaultsSelected()
        {
            sketch = new CmSketch<int, I>(0, EqualityComparer<int>.Default);

            sketch.ResetSampleSize.Should().Be(10);
        }

        [SkippableFact]
        public void WhenIncrementedOnceCountIsOne()
        {
            sketch.Increment(1);
            sketch.EstimateFrequency(1).Should().Be(1);
        }

        [SkippableFact]
        public void WhenIncrementedMoreThanMaxCountIsMaximum()
        {
            for (int i = 0; i < 20; i++)
            {
                sketch.Increment(1);
            }

            sketch.EstimateFrequency(1).Should().Be(15);
        }

        [SkippableFact]
        public void WhenTwoItemsIncrementedCountIsIndependent()
        {
            sketch.Increment(1);
            sketch.Increment(1);
            sketch.Increment(2);

            sketch.EstimateFrequency(1).Should().Be(2);
            sketch.EstimateFrequency(2).Should().Be(1);
        }

        [SkippableFact]
        public void WhenSampleSizeExceededCountIsReset()
        {
            bool reset = false;

            sketch = new CmSketch<int, I>(64, EqualityComparer<int>.Default);

            for (int i = 1; i < 20 * 64; i++)
            {
                sketch.Increment(i);

                if (sketch.Size != i)
                {
                    i.Should().NotBe(1, "sketch should not be reset on the first iteration. Resize logic is broken");

                    reset = true;
                    break;
                }
            }

            reset.Should().BeTrue();
            sketch.Size.Should().BeLessThan(10 * 64);
        }

        [SkippableFact]
        public void WhenClearedCountIsReset()
        {
            sketch.Increment(1);
            sketch.Increment(1);
            sketch.Increment(2);

            sketch.Clear();

            sketch.EstimateFrequency(1).Should().Be(0);
            sketch.EstimateFrequency(2).Should().Be(0);
        }

        private static void SkipAvxIfNotSupported()
        {
#if !NET48
            // when we are trying to test Avx2, skip the test if it's not supported
            Skip.If(typeof(I) == typeof(DetectIsa) && !Avx2.IsSupported);
#endif
        }
    }
}
