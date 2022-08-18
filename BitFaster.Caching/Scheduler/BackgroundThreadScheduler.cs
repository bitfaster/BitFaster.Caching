﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
