using System;
using System.Collections.Generic;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace BitFaster.Caching.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, GetGlobalConfig(args));
        }

        // This gives a default where we run both net48 and net9.0 unless overridden on the command line.
        static IConfig GetGlobalConfig(string[] args)
        {
            //if args contains either --runtimes or --r, return default config  
            foreach (var a in args)
            {
                if (a == "--runtimes" || a == "--r")
                {
                    return DefaultConfig.Instance;
                }
            }

            // else default to both net48 and net9.0
            return DefaultConfig.Instance
#if Windows
                .AddJob(
                    Job.Default
                        .WithRuntime(ClrRuntime.Net48)
                        .WithId("net48"))
#endif
                .AddJob(
                    Job.Default
                        .WithRuntime(CoreRuntime.Core90)
                        .WithId("net9.0")
                        .AsDefault());

        }
    }
}
