using System;
using System.Threading.Tasks;

namespace BitFaster.Caching.UnitTests
{
    public static class TaskExtensions
    {
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout, string message)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)))
                await task;
            else
                throw new TimeoutException(message);
        }
    }
}
