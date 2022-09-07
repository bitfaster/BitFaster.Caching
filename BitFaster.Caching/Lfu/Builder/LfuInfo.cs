using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Lru;
using BitFaster.Caching.Scheduler;

namespace BitFaster.Caching.Lfu.Builder
{
    internal sealed class LfuInfo<K>
    {
        private LfuBufferSize bufferConfiguration;

        public int Capacity { get; set; } = 128;

        public int ConcurrencyLevel { get; set; } = Defaults.ConcurrencyLevel;

        public IScheduler Scheduler { get; set; } = new ThreadPoolScheduler();

        public IEqualityComparer<K> KeyComparer { get; set; } = EqualityComparer<K>.Default;

        public LfuBufferSize BufferConfiguration 
        {
            get
            { 
                return this.bufferConfiguration ?? LfuBufferSize.Default(ConcurrencyLevel, Capacity); 
            }
            set
            { 
                bufferConfiguration = value; 
            }
        }
    }
}
