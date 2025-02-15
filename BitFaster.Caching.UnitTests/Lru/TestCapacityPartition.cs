using BitFaster.Caching.Lru;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class TestCapacityPartition : ICapacityPartition
    {
        public int Cold { get; set; }

        public int Warm { get; set; }

        public int Hot { get; set; }
    }
}
