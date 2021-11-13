using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public interface IPolicy<in K, in V, I> where I : LruItem<K, V>
    {
        I CreateItem(K key, V value);

        void Touch(I item);

        bool ShouldDiscard(I item);

        ItemDestination RouteHot(I item);

        ItemDestination RouteWarm(I item);

        ItemDestination RouteCold(I item);
    }

    // now item is wrapped in W via policy
    public interface IItemPolicy<in K, in V, in W, I> where I : LruItem<K, W>
    {
        I CreateItem(K key, V value);

        void Touch(I item);

        bool ShouldDiscard(I item);

        ItemDestination RouteHot(I item);

        ItemDestination RouteWarm(I item);

        ItemDestination RouteCold(I item);
    }
}
