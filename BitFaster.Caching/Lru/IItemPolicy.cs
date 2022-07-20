using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
    public interface IItemPolicy<in K, in V, I> where I : LruItem<K, V>
    {
        I CreateItem(K key, V value);

        void Touch(I item);

        void Update(I item);

        bool ShouldDiscard(I item);

        bool CanDiscard();

        ItemDestination RouteHot(I item);

        ItemDestination RouteWarm(I item);

        ItemDestination RouteCold(I item);
    }
}
