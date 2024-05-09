using System;
using System.Threading;

namespace BitFaster.Caching.HitRateAnalysis
{
    internal class ThreadPoolInspector
    {
        public static void WaitForEmpty()
        {
            while (ThreadPool.PendingWorkItemCount > 0)
            {
                Thread.Yield();

                // This is very hacky, but by experimentation 300 Sleep(0) consistently takes longer 
                // than cache maintenance giving stable results with around 25% run time penalty.
                // Sleep(1) makes the test take 50x longer.
                if (ThreadPool.PendingWorkItemCount == 0)
                {
                    for (int i = 0; i < 300; i++)
                    {
                        Thread.Sleep(0);
                    }
                }
            }
        }
    }
}
