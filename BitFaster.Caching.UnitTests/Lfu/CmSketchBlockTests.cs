
using System.Collections.Generic;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    // Test with AVX2 if it is supported
    public class CMSketchBlockAvx2Tests : CmSketchBlockTestBase<DetectIsa>
    {
    }

    // Test with AVX2 disabled
    public class CmSketchBlockTests : CmSketchBlockTestBase<DisableHardwareIntrinsics>
    {
    }

    public abstract class CmSketchBlockTestBase<I> where I : struct, IsaProbe
    {
        private CmSketchBlock<int, I> sketch = new CmSketchBlock<int, I>(512, EqualityComparer<int>.Default);

        public CmSketchBlockTestBase()
        {
            Intrinsics.SkipAvxIfNotSupported<I>();
        }

        [SkippableFact]
        public void WhenCapacityIsZeroDefaultsSelected()
        {
            sketch = new CmSketchBlock<int, I>(0, EqualityComparer<int>.Default);

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
        public void WhenTwoItemsIncrementedCountIsIndependent2()
        {
            sketch.Increment(1);
            sketch.Increment(1);
            sketch.Increment(2);

            var (a, b) = sketch.EstimateFrequency(1, 2);
            a.Should().Be(2);
            b.Should().Be(1);
        }

        [SkippableFact]
        public void WhenSampleSizeExceededCountIsReset()
        {
            bool reset = false;

            sketch = new CmSketchBlock<int, I>(64, EqualityComparer<int>.Default);

            for (int i = 1; i < 20 * 64; i++)
            {
                sketch.Increment(i);

                if (sketch.Size != i)
                {
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
    }
}
