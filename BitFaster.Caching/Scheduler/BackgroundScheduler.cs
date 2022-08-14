using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Scheduler
{
    /// <summary>
    /// Represents a scheduler that handles queuing tasks to execute in the ThreadPool.
    /// </summary>
    public class BackgroundScheduler : IScheduler
    {
        private long count;
        private Optional<Exception> lastException = Optional<Exception>.None();

#if NETSTANDARD2_0 || NETCOREAPP3_1
        private TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
#else
        private TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
#endif

        public long RunCount => count;

        public Task Next => taskCompletionSource.Task;

        public Optional<Exception> LastException => lastException;

        public void Run(Action action)
        {
            count++;
            var task = Task.Run(action);
            task.ContinueWith(t => lastException = new Optional<Exception>(t.Exception.Flatten().InnerException), TaskContinuationOptions.OnlyOnFaulted);

#if NETSTANDARD2_0 || NETCOREAPP3_1
            task.ContinueWith(t => taskCompletionSource.SetResult(true));
            taskCompletionSource = new TaskCompletionSource<bool>();
#else
            task.ContinueWith(t => taskCompletionSource.SetResult());
            taskCompletionSource = new TaskCompletionSource();
#endif
        }
    }
}
