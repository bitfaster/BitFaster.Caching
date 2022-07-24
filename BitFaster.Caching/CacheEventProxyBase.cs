using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public abstract class CacheEventProxyBase<K, TInner, TOuter> : ICacheEvents<K, TOuter>
    {
        private readonly ICacheEvents<K, TInner> events;

/* Unmerged change from project 'BitFaster.Caching (netcoreapp3.1)'
Before:
        private event EventHandler<Lru.ItemRemovedEventArgs<K, TOuter>> itemRemovedProxy;
After:
        private event EventHandler<ItemRemovedEventArgs<K, TOuter>> itemRemovedProxy;
*/
        private event EventHandler<Caching.ItemRemovedEventArgs<K, TOuter>> itemRemovedProxy;

        public CacheEventProxyBase(ICacheEvents<K, TInner> events)
        {
            this.events = events;
        }

        public bool IsEnabled => this.events.IsEnabled;


/* Unmerged change from project 'BitFaster.Caching (netcoreapp3.1)'
Before:
        public event EventHandler<Lru.ItemRemovedEventArgs<K, TOuter>> ItemRemoved
After:
        public event EventHandler<ItemRemovedEventArgs<K, TOuter>> ItemRemoved
*/
        public event EventHandler<Caching.ItemRemovedEventArgs<K, TOuter>> ItemRemoved
        {
            add { this.Register(value); }
            remove { this.UnRegister(value); }
        }


/* Unmerged change from project 'BitFaster.Caching (netcoreapp3.1)'
Before:
        private void Register(EventHandler<Lru.ItemRemovedEventArgs<K, TOuter>> value)
After:
        private void Register(EventHandler<ItemRemovedEventArgs<K, TOuter>> value)
*/
        private void Register(EventHandler<Caching.ItemRemovedEventArgs<K, TOuter>> value)
        {
            itemRemovedProxy += value;
            events.ItemRemoved += OnItemRemoved;
        }


/* Unmerged change from project 'BitFaster.Caching (netcoreapp3.1)'
Before:
        private void UnRegister(EventHandler<Lru.ItemRemovedEventArgs<K, TOuter>> value)
After:
        private void UnRegister(EventHandler<ItemRemovedEventArgs<K, TOuter>> value)
*/
        private void UnRegister(EventHandler<Caching.ItemRemovedEventArgs<K, TOuter>> value)
        {
            this.itemRemovedProxy -= value;

            if (this.itemRemovedProxy == null)
            {
                this.events.ItemRemoved -= OnItemRemoved;
            }
        }


/* Unmerged change from project 'BitFaster.Caching (netcoreapp3.1)'
Before:
        private void OnItemRemoved(object sender, Lru.ItemRemovedEventArgs<K, TInner> args)
After:
        private void OnItemRemoved(object sender, ItemRemovedEventArgs<K, TInner> args)
*/
        private void OnItemRemoved(object sender, Caching.ItemRemovedEventArgs<K, TInner> args)
        {
            itemRemovedProxy(sender, TranslateOnRemoved(args));
        }

        protected abstract ItemRemovedEventArgs<K, TOuter> TranslateOnRemoved(ItemRemovedEventArgs<K, TInner> inner);
    }
}
