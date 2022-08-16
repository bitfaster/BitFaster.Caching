using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitFaster.Caching.Scheduler
{

    // GetOrAdd bench: this is about 2x slower than no background work (acceptable perf), but Dispose hangs benchmarkdotnet.
    //
    //|               Method |            Runtime |      Mean |     Error |     StdDev | Ratio | Code Size | Allocated |
    //|--------------------- |------------------- |----------:|----------:|-----------:|------:|----------:|----------:|
    //| ConcurrentDictionary |           .NET 6.0 |  7.756 ns | 0.0444 ns |  0.0415 ns |  1.00 |   1,523 B |         - |
    //|        ConcurrentLfu |           .NET 6.0 | 31.013 ns | 1.9952 ns |  5.8829 ns |  3.80 |   5,885 B |         - |
    //|                      |                    |           |           |            |       |           |           |
    //| ConcurrentDictionary | .NET Framework 4.8 | 14.496 ns | 0.1214 ns |  0.1135 ns |  1.00 |   4,207 B |         - |
    //|        ConcurrentLfu | .NET Framework 4.8 | 78.272 ns | 4.8612 ns | 14.3335 ns |  4.86 |  10,910 B |         - |
    
    // with buffered read 1024
    //|               Method |            Runtime |      Mean |     Error |    StdDev | Ratio | Code Size | Allocated |
    //|--------------------- |------------------- |----------:|----------:|----------:|------:|----------:|----------:|
    //| ConcurrentDictionary |           .NET 6.0 |  7.763 ns | 0.0636 ns | 0.0564 ns |  1.00 |   1,523 B |         - |
    //|        ConcurrentLfu |           .NET 6.0 | 15.858 ns | 0.1615 ns | 0.1511 ns |  2.04 |   7,569 B |         - |
    //|                      |                    |           |           |           |       |           |           |
    //| ConcurrentDictionary | .NET Framework 4.8 | 13.671 ns | 0.1372 ns | 0.1145 ns |  1.00 |   4,207 B |         - |
    //|        ConcurrentLfu | .NET Framework 4.8 | 21.096 ns | 0.1381 ns | 0.1292 ns |  1.54 |  10,658 B |         - |

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Goals:
    /// 1. Background thread awaits work, does not block a thread pool thread.
    /// 2. Does not allocate when scheduling
    /// 3. Is faster than Task.Run/TaskFactory.StartNew
    public sealed class BackgroundThreadScheduler : IScheduler, IDisposable
    {
        private const int MaxBacklog = 16;
        private int count;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0, MaxBacklog);
        private readonly BoundedBuffer<Action> work = new BoundedBuffer<Action>(MaxBacklog);

        private Optional<Exception> lastException = Optional<Exception>.None();

        TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();

        public BackgroundThreadScheduler()
        {
            // dedicated thread
            Task.Factory.StartNew(() => Background(), TaskCreationOptions.LongRunning);
        }

        public Task Completion => completion.Task;

        public bool IsBackground => true;

        public long RunCount => count;

        public Optional<Exception> LastException => lastException;

        public void Run(Action action)
        {
            count++;
            if (work.TryAdd(action))
            {
                semaphore.Release();
            }
            else
            {
                throw new InvalidOperationException($"More than {MaxBacklog} tasks scheduled");
            }
        }

        private async Task Background()
        {
            var spinner = new SpinWait();

            while (true)
            {
                try
                {
                    await semaphore.WaitAsync(cts.Token);

                    if (work.TryTake(out var action))
                    {
                        action();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    this.lastException = new Optional<Exception>(ex);
                }

                spinner.SpinOnce();
            }

            completion.SetResult(true);
        }

        public void Dispose()
        {
            // prevent hang when cancel runs on the same thread
            this.cts.CancelAfter(TimeSpan.Zero);
        }
    }
}
