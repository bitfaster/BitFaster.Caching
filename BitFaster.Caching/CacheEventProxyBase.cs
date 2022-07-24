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

        private event EventHandler<ItemRemovedEventArgs<K, TOuter>> itemRemovedProxy;

        public CacheEventProxyBase(ICacheEvents<K, TInner> events)
        {
            this.events = events;
        }

        public bool IsEnabled => this.events.IsEnabled;

        public event EventHandler<ItemRemovedEventArgs<K, TOuter>> ItemRemoved
        {
            add { this.Register(value); }
            remove { this.UnRegister(value); }
        }

        private void Register(EventHandler<ItemRemovedEventArgs<K, TOuter>> value)
        {
            itemRemovedProxy += value;
            events.ItemRemoved += OnItemRemoved;
        }

        private void UnRegister(EventHandler<ItemRemovedEventArgs<K, TOuter>> value)
        {
            this.itemRemovedProxy -= value;

            if (this.itemRemovedProxy == null)
            {
                this.events.ItemRemoved -= OnItemRemoved;
            }
        }

        private void OnItemRemoved(object sender, ItemRemovedEventArgs<K, TInner> args)
        {
            itemRemovedProxy(sender, TranslateOnRemoved(args));
        }

        protected abstract ItemRemovedEventArgs<K, TOuter> TranslateOnRemoved(ItemRemovedEventArgs<K, TInner> inner);
    }
}
