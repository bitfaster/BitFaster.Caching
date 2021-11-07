using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class ValueFactory
    {
        public int timesCalled;

        public string Create(int key)
        {
            timesCalled++;
            return key.ToString();
        }

        public Task<string> CreateAsync(int key)
        {
            timesCalled++;
            return Task.FromResult(key.ToString());
        }
    }
}
