using System;
using System.Collections.Generic;
using Perfolizer.Mathematics.Common;
using Perfolizer.Mathematics.OutlierDetection;

namespace BitFaster.Caching.ThroughputAnalysis
{
    // https://github.com/dotnet/BenchmarkDotNet/blob/b4ac9df9f7890ca9669e2b9c8835af35c072a453/src/BenchmarkDotNet/Mathematics/MeasurementsStatistics.cs#L13
    internal readonly ref struct MeasurementsStatistics
    {
        /// <summary>
        /// Standard error in nanoseconds.
        /// </summary>
        public double StandardError { get; }

        /// <summary>
        /// Mean in nanoseconds.
        /// </summary>
        public double Mean { get; }

        /// <summary>
        /// 99.9% confidence interval in nanoseconds.
        /// </summary>
        public ConfidenceInterval ConfidenceInterval { get; }

        private MeasurementsStatistics(double standardError, double mean, ConfidenceInterval confidenceInterval)
        {
            StandardError = standardError;
            Mean = mean;
            ConfidenceInterval = confidenceInterval;
        }

        public static MeasurementsStatistics Calculate(List<double> measurements, OutlierMode outlierMode)
        {
            int n = measurements.Count;
            if (n == 0)
                throw new InvalidOperationException("StatSummary: Sequence contains no elements");

            double sum = Sum(measurements);
            double mean = sum / n;

            double variance = Variance(measurements, n, mean);
            double standardDeviation = Math.Sqrt(variance);
            double standardError = standardDeviation / Math.Sqrt(n);
            var confidenceInterval = new ConfidenceInterval(mean, standardError, n);

            if (outlierMode == OutlierMode.DontRemove) // most simple scenario is done without allocations! but this is not the default case
                return new MeasurementsStatistics(standardError, mean, confidenceInterval);

            measurements.Sort(); // sort in place

            double q1, q3;

            if (n == 1)
                q1 = q3 = measurements[0];
            else
            {
                q1 = GetQuartile(measurements, measurements.Count / 2);
                q3 = GetQuartile(measurements, measurements.Count * 3 / 2);
            }

            double interquartileRange = q3 - q1;
            double lowerFence = q1 - 1.5 * interquartileRange;
            double upperFence = q3 + 1.5 * interquartileRange;

            SumWithoutOutliers(outlierMode, measurements, lowerFence, upperFence, out sum, out n); // updates sum and N
            mean = sum / n;

            variance = VarianceWithoutOutliers(outlierMode, measurements, n, mean, lowerFence, upperFence);
            standardDeviation = Math.Sqrt(variance);
            standardError = standardDeviation / Math.Sqrt(n);
            confidenceInterval = new ConfidenceInterval(mean, standardError, n);

            return new MeasurementsStatistics(standardError, mean, confidenceInterval);
        }

        private static double Sum(List<double> measurements)
        {
            double sum = 0;
            foreach (var m in measurements)
                sum += m;
            return sum;
        }

        private static void SumWithoutOutliers(OutlierMode outlierMode, List<double> measurements,
            double lowerFence, double upperFence, out double sum, out int n)
        {
            sum = 0;
            n = 0;

            foreach (var m in measurements)
                if (!IsOutlier(outlierMode, m, lowerFence, upperFence))
                {
                    sum += m;
                    ++n;
                }
        }

        private static double Variance(List<double> measurements, int n, double mean)
        {
            if (n == 1)
                return 0;

            double variance = 0;
            foreach (var m in measurements)
                variance += (m - mean) * (m - mean) / (n - 1);

            return variance;
        }

        private static double VarianceWithoutOutliers(OutlierMode outlierMode, List<double> measurements, int n, double mean, double lowerFence, double upperFence)
        {
            if (n == 1)
                return 0;

            double variance = 0;
            foreach (var m in measurements)
                if (!IsOutlier(outlierMode, m, lowerFence, upperFence))
                    variance += (m - mean) * (m - mean) / (n - 1);

            return variance;
        }

        private static double GetQuartile(List<double> measurements, int count)
        {
            if (count % 2 == 0)
                return (measurements[count / 2 - 1] + measurements[count / 2]) / 2;

            return measurements[count / 2];
        }

        private static bool IsOutlier(OutlierMode outlierMode, double value, double lowerFence, double upperFence)
        {
            switch (outlierMode)
            {
                case OutlierMode.DontRemove:
                    return false;
                case OutlierMode.RemoveUpper:
                    return value > upperFence;
                case OutlierMode.RemoveLower:
                    return value < lowerFence;
                case OutlierMode.RemoveAll:
                    return value < lowerFence || value > upperFence;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outlierMode), outlierMode, null);
            }
        }
    }
}
