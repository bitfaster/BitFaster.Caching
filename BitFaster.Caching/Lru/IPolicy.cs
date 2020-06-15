using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching.Lru
{
	public interface IPolicy<in K, in V, I> where I : LruItem<K, V>
	{
        DateTime UtcNow();

		I CreateItem(K key, V value);

		void Touch(I item);

		bool ShouldDiscard(I item, ref DateTime now);

		ItemDestination RouteHot(I item, ref DateTime now);

		ItemDestination RouteWarm(I item, ref DateTime now);

		ItemDestination RouteCold(I item, ref DateTime now);
	}
}
