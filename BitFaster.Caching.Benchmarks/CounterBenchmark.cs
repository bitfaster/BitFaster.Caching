
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Benchly;
using BenchmarkDotNet.Attributes;

namespace BitFaster.Caching.Benchmarks
{
#if Windows
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
#endif
    [HideColumns("Job", "Median", "RatioSD", "Alloc Ratio")]
    [ColumnChart(Title = "Counter Latency ({JOB})", Output = OutputMode.PerJob, Colors = "seagreen,darkgreen,thistle,plum,lightcoral,indianred,lightpink,hotpink")]
    public class CounterBenchmark
    {
        const int Iters = 1_000_000;

        private Counters.Counter counter = new Counters.Counter();

        private Striped64Counter striped64Counter = new Striped64Counter();

        private Meter meter;
        private Counter<long> metricsCounter;
        private UpDownCounter<long> upDownCounter;
        private MetricsEventListener listener;

        [GlobalSetup]
        public void Setup()
        {
            meter = new Meter("Example");
            upDownCounter = meter.CreateUpDownCounter<long>("upDownCounter");
            metricsCounter = meter.CreateCounter<long>("counter");
            listener = new MetricsEventListener();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            meter.Dispose();
        }

        [Benchmark]
        public void CounterSerial()
        {
            for (int i = 0; i < Iters; i++)
            {
                counter.Add(1);
                counter.Add(1);
            }
        }

        [Benchmark]
        public void CounterParallel()
        {
            Parallel.For(0, Iters, i =>
            {
                counter.Add(1);
                counter.Add(1);
            });
        }

        [Benchmark]
        public void Striped64CounterSerial()
        {
            for (int i = 0; i < Iters; i++)
            {
                striped64Counter.Add(1);
                striped64Counter.Add(1);
            }
        }

        [Benchmark]
        public void Striped64CounterParallel()
        {
            Parallel.For(0, Iters, i =>
            {
                striped64Counter.Add(1);
                striped64Counter.Add(1);
            });
        }

        [Benchmark]
        public void MetricsCounterSerial()
        {
            for (int i = 0; i < Iters; i++)
            {
                metricsCounter.Add(1);
                metricsCounter.Add(1);
            }
        }

        [Benchmark]
        public void MetricsCounterParallel()
        {
            Parallel.For(0, Iters, i =>
            {
                metricsCounter.Add(1);
                metricsCounter.Add(1);
            });
        }

        [Benchmark]
        public void UpDownCounterSerial()
        {
            for (int i = 0; i < Iters; i++)
            {
                upDownCounter.Add(1);
                upDownCounter.Add(-1);
            }
        }

        [Benchmark]
        public void UpDownCounterParallel()
        {
            Parallel.For(0, Iters, i =>
            {
                upDownCounter.Add(1);
                upDownCounter.Add(-1);
            });
        }

        private sealed class MetricsEventListener : EventListener
        {
            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "System.Diagnostics.Metrics")
                {
                    EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All, new Dictionary<string, string>() { { "Metrics", "Example\\upDownCounter;Example\\counter" } });
                }
            }
        }
    }
}
