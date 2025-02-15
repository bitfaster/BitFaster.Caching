using System.Collections.Generic;
using BitFaster.Caching.Lfu;
using Shouldly;
using Xunit;

namespace BitFaster.Caching.UnitTests.Lfu
{
    // Test with AVX2/ARM64 if it is supported
    public class CMSketchIntrinsicsTests : CmSketchTestBase<DetectIsa>
    {
    }

    // Test with AVX2/ARM64 disabled
    public class CmSketchTests : CmSketchTestBase<DisableHardwareIntrinsics>
    {
    }

    public abstract class CmSketchTestBase<I> where I : struct, IsaProbe
    {
        private CmSketchCore<int, I> sketch = new CmSketchCore<int, I>(512, EqualityComparer<int>.Default);

        public CmSketchTestBase()
        {
            Intrinsics.SkipAvxIfNotSupported<I>();
        }

        [SkippableFact]
        public void Repro()
        {
            sketch = new CmSketchCore<int, I>(1_048_576, EqualityComparer<int>.Default);
            var baseline = new CmSketchCore<int, DisableHardwareIntrinsics>(1_048_576, EqualityComparer<int>.Default);

            for (int i = 0; i < 1_048_576; i++)
            {
                if (i % 3 == 0)
                {
                    sketch.Increment(i);
                    baseline.Increment(i);
                }
            }

            baseline.Size.ShouldBe(sketch.Size);

            for (int i = 0; i < 1_048_576; i++)
            {
                sketch.EstimateFrequency(i).ShouldBe(baseline.EstimateFrequency(i));
            }
        }


        [SkippableFact]
        public void WhenCapacityIsZeroDefaultsSelected()
        {
            sketch = new CmSketchCore<int, I>(0, EqualityComparer<int>.Default);

            sketch.ResetSampleSize.ShouldBe(10);
        }

        [SkippableFact]
        public void WhenIncrementedOnceCountIsOne()
        {
            sketch.Increment(1);
            sketch.EstimateFrequency(1).ShouldBe(1);
        }

        [SkippableFact]
        public void WhenIncrementedMoreThanMaxCountIsMaximum()
        {
            for (int i = 0; i < 20; i++)
            {
                sketch.Increment(1);
            }

            sketch.EstimateFrequency(1).ShouldBe(15);
        }

        [SkippableFact]
        public void WhenTwoItemsIncrementedCountIsIndependent()
        {
            sketch.Increment(1);
            sketch.Increment(1);
            sketch.Increment(2);

            sketch.EstimateFrequency(1).ShouldBe(2);
            sketch.EstimateFrequency(2).ShouldBe(1);
        }

        [SkippableFact]
        public void WhenSampleSizeExceededCountIsReset()
        {
            bool reset = false;

            sketch = new CmSketchCore<int, I>(64, EqualityComparer<int>.Default);

            for (int i = 1; i < 20 * 64; i++)
            {
                sketch.Increment(i);

                if (sketch.Size != i)
                {
                    i.ShouldNotBe(1, "sketch should not be reset on the first iteration. Resize logic is broken");

                    reset = true;
                    break;
                }
            }

            reset.ShouldBeTrue();
            sketch.Size.ShouldBeLessThan(10 * 64);
        }

        [SkippableFact]
        public void WhenClearedCountIsReset()
        {
            sketch.Increment(1);
            sketch.Increment(1);
            sketch.Increment(2);

            sketch.Clear();

            sketch.EstimateFrequency(1).ShouldBe(0);
            sketch.EstimateFrequency(2).ShouldBe(0);
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
                    popularity[i].ShouldBeLessThanOrEqualTo(popularity[2]);
                }
                else if (i == 2)
                {
                    popularity[2].ShouldBeLessThanOrEqualTo(popularity[4]);
                }
                else if (i == 4)
                {
                    popularity[4].ShouldBeLessThanOrEqualTo(popularity[6]);
                }
                else if (i == 6)
                {
                    popularity[6].ShouldBeLessThanOrEqualTo(popularity[8]);
                }
            }
        }
    }
}
