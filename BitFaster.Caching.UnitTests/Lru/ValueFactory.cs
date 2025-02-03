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

        public string Create<TArg>(int key, TArg arg)
        {
            timesCalled++;
            return $"{key}{arg}";
        }

        public Task<string> CreateAsync(int key)
        {
            timesCalled++;
            return Task.FromResult(key.ToString());
        }

        public Task<string> CreateAsync<TArg>(int key, TArg arg)
        {
            timesCalled++;
            return Task.FromResult($"{key}{arg}");
        }
    }
}
