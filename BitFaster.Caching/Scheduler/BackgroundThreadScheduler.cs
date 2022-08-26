using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BitFaster.Caching.Buffers;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that handles queuing tasks on a long running background thread.
    /// </summary>
    /// <remarks>
    /// Goals:
    /// 1. Background thread awaits work, does not block a thread pool thread.
    /// 2. Does not allocate when scheduling.
    /// 3. Is faster than Task.Run/TaskFactory.StartNew.
    /// </remarks>
    public sealed class BackgroundThreadScheduler : IScheduler, IDisposable
    {
        public const int MaxBacklog = 16;
        private int count;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0, MaxBacklog);
        private readonly MpmcBoundedBuffer<Action> work = new MpmcBoundedBuffer<Action>(MaxBacklog);

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
            BufferStatus s;

            //do
            {
                s = work.TryAdd(action);
            }
            //while (s == Status.Contended);

            if (s == BufferStatus.Success)
            {

                semaphore.Release();
                count++;
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

                    BufferStatus s;
                    do
                    {
                        s = work.TryTake(out var action);

                        if (s == BufferStatus.Success)
                        {
                            action();
                        }
                        else 
                        {
                            spinner.SpinOnce();
                        }
                    }
                    while (s == BufferStatus.Contended);
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
