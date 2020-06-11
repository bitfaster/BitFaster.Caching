using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner
                .Run<Lightweight.Caching3.Benchmarks.LruGetOrAddTest>(ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(Job.RyuJitX64));
        }
    }
}
