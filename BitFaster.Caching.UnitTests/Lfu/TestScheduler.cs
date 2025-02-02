using System;
using System.Collections.Concurrent;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.UnitTests.Lfu
{
    public class TestScheduler : IScheduler
    {
        private int count = 0;
        private ConcurrentQueue<Action> work = new ConcurrentQueue<Action>();

        public bool IsBackground => true;

        public long RunCount => count;

        public Optional<Exception> LastException => Optional<Exception>.None();

        public IProducerConsumerCollection<Action> Work => this.work;

        public void Run(Action action)
        {
            this.count++;
            work.Enqueue(action);
        }
    }
}
