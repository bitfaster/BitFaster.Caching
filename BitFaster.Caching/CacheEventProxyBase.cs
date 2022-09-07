using System;

namespace BitFaster.Caching
{
    /// <summary>
    /// Represents a base class for converting cache events for decorated caches such that the inner cache wrapper
    /// type can be hidden from the outer cache.
    /// </summary>
    /// <typeparam name="K">The type of the key</typeparam>
    /// <typeparam name="TInner">The inner value type</typeparam>
    /// <typeparam name="TOuter">The outer value type</typeparam>
    public abstract class CacheEventProxyBase<K, TInner, TOuter> : ICacheEvents<K, TOuter>
    {
        private readonly ICacheEvents<K, TInner> events;

        private event EventHandler<ItemRemovedEventArgs<K, TOuter>> itemRemovedProxy;

        /// <summary>
        /// Initializes a new instance of the CacheEventProxyBase class with the specified inner cache events.
        /// </summary>
        /// <param name="events">The inner cache events.</param>
        public CacheEventProxyBase(ICacheEvents<K, TInner> events)
        {
            this.events = events;
        }

        ///<inheritdoc/>
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

        /// <summary>
        /// Translate the ItemRemovedEventArgs by converting the inner arg type to the outer arg type.
        /// </summary>
        /// <param name="inner">The inner arg.</param>
        /// <returns>The translated arg.</returns>
        protected abstract ItemRemovedEventArgs<K, TOuter> TranslateOnRemoved(ItemRemovedEventArgs<K, TInner> inner);
    }
}
