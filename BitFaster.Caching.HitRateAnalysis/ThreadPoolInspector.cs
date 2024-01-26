using System;
using System.Threading;

namespace BitFaster.Caching.HitRateAnalysis
{
    internal class ThreadPoolInspector
    {
        public static void WaitForEmpty()
        {
            int count = 0;
            while (ThreadPool.PendingWorkItemCount > 0)
            {
                Thread.Yield();
                Thread.Sleep(1);

                if (count++ > 10)
                {
                    Console.WriteLine("Waiting for thread pool to flush...");
                }
            }
        }
    }
}
