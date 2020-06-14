using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.UnitTests.Lru
{
    public class DisposableValueFactory
    {
        private Dictionary<int, DisposableItem> items = new Dictionary<int, DisposableItem>();

        public Dictionary<int, DisposableItem> Items => this.items;

        public DisposableItem Create(int key)
        {
            var item = new DisposableItem();
            items.Add(key, item);
            return item;
        }

        public Task<DisposableItem> CreateAsync(int key)
        {
            var item = new DisposableItem();
            items.Add(key, item);
            return Task.FromResult(item);
        }
    }
}
