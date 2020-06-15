using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BitFaster.Caching.Benchmarks.Lru;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner
                .Run<LruCycle>(ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(Job.RyuJitX64));
        }
    }
}
