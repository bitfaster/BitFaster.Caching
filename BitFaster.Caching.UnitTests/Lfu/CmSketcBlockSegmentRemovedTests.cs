
using System.Collections.Generic;
using BitFaster.Caching.Lfu;
using FluentAssertions;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    // Test with AVX2 if it is supported
    public class CMSketchBlockSegmentRemovedAvx2Tests : CmSketcBlockSegmentRemovedTestBase<DetectIsa>
    {
    }

    // Test with AVX2 disabled
    public class CmSketchBlockSegmentRemovedTests : CmSketcBlockSegmentRemovedTestBase<DisableHardwareIntrinsics>
    {
    }

    public abstract class CmSketcBlockSegmentRemovedTestBase<I> where I : struct, IsaProbe
    {
        private CmSketchBlockSegmentRemoved<int, I> sketch = new CmSketchBlockSegmentRemoved<int, I>(512, EqualityComparer<int>.Default);

        public CmSketcBlockSegmentRemovedTestBase()
        {
            Intrinsics.SkipAvxIfNotSupported<I>();
        }

        [SkippableFact]
        public void WhenCapacityIsZeroDefaultsSelected()
        {
            sketch = new CmSketchBlockSegmentRemoved<int, I>(0, EqualityComparer<int>.Default);

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

        ////[SkippableFact]
        ////public void WhenTwoItemsIncrementedCountIsIndependent2()
        ////{
        ////    sketch.Increment(1);
        ////    sketch.Increment(1);
        ////    sketch.Increment(2);

        ////    var (a, b) = sketch.EstimateFrequency(1, 2);
        ////    a.Should().Be(2);
        ////    b.Should().Be(1);
        ////}

        [SkippableFact]
        public void WhenSampleSizeExceededCountIsReset()
        {
            bool reset = false;

            sketch = new CmSketchBlockSegmentRemoved<int, I>(64, EqualityComparer<int>.Default);

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

        [SkippableFact]
        public void HeavyHitters()
        {
            for (int i = 100; i < 100_000; i++)
            {
                sketch.Increment(i);
            }
            for (int i = 0; i < 10; i += 2)
            {
                for (int j = 0; j < i; j++)
                {
                    sketch.Increment(i);
                }
            }

            // A perfect popularity count yields an array [0, 0, 2, 0, 4, 0, 6, 0, 8, 0]
            int[] popularity = new int[10];

            for (int i = 0; i < 10; i++)
            {
                popularity[i] = sketch.EstimateFrequency(i);
            }

            for (int i = 0; i < popularity.Length; i++)
            {
                if ((i == 0) || (i == 1) || (i == 3) || (i == 5) || (i == 7) || (i == 9))
                {
                    popularity[i].Should().BeLessThanOrEqualTo(popularity[2]);
                }
                else if (i == 2)
                {
                    popularity[2].Should().BeLessThanOrEqualTo(popularity[4]);
                }
                else if (i == 4)
                {
                    popularity[4].Should().BeLessThanOrEqualTo(popularity[6]);
                }
                else if (i == 6)
                {
                    popularity[6].Should().BeLessThanOrEqualTo(popularity[8]);
                }
            }
        }
    }
}
