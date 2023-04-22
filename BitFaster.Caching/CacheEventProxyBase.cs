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

        private event EventHandler<ItemUpdatedEventArgs<K, TOuter>> itemUpdatedProxy;

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
            add { this.RegisterRemoved(value); }
            remove { this.UnRegisterRemoved(value); }
        }

        ///<inheritdoc/>
        public event EventHandler<ItemUpdatedEventArgs<K, TOuter>> ItemUpdated
        {
            add { this.RegisterUpdated(value); }
            remove { this.UnRegisterUpdated(value); }
        }

        private void RegisterRemoved(EventHandler<ItemRemovedEventArgs<K, TOuter>> value)
        {
            itemRemovedProxy += value;
            events.ItemRemoved += OnItemRemoved;
        }

        private void UnRegisterRemoved(EventHandler<ItemRemovedEventArgs<K, TOuter>> value)
        {
            this.itemRemovedProxy -= value;

            if (this.itemRemovedProxy == null)
            {
                this.events.ItemRemoved -= OnItemRemoved;
            }
        }

        private void RegisterUpdated(EventHandler<ItemUpdatedEventArgs<K, TOuter>> value)
        {
            itemUpdatedProxy += value;
            events.ItemUpdated += OnItemUpdated;
        }

        private void UnRegisterUpdated(EventHandler<ItemUpdatedEventArgs<K, TOuter>> value)
        {
            this.itemUpdatedProxy -= value;

            if (this.itemUpdatedProxy == null)
            {
                this.events.ItemUpdated -= OnItemUpdated;
            }
        }

        private void OnItemRemoved(object sender, ItemRemovedEventArgs<K, TInner> args)
        {
            itemRemovedProxy(sender, TranslateOnRemoved(args));
        }

        private void OnItemUpdated(object sender, ItemUpdatedEventArgs<K, TInner> args)
        {
            itemUpdatedProxy(sender, TranslateOnUpdated(args));
        }

        /// <summary>
        /// Translate the ItemRemovedEventArgs by converting the inner arg type to the outer arg type.
        /// </summary>
        /// <param name="inner">The inner arg.</param>
        /// <returns>The translated arg.</returns>
        protected abstract ItemRemovedEventArgs<K, TOuter> TranslateOnRemoved(ItemRemovedEventArgs<K, TInner> inner);

        /// <summary>
        /// Translate the ItemUpdatedEventArgs by converting the inner arg type to the outer arg type.
        /// </summary>
        /// <param name="inner">The inner arg.</param>
        /// <returns>The translated arg.</returns>
        protected virtual ItemUpdatedEventArgs<K, TOuter> TranslateOnUpdated(ItemUpdatedEventArgs<K, TInner> inner)
        {
            return new ItemUpdatedEventArgs<K, TOuter>(inner.Key, default(TOuter), default(TOuter));
        }
    }
}
