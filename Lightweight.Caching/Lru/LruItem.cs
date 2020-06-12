using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
	public class LruItem<K, V>
	{
		private bool wasAccessed;

		public LruItem(K k, V v)
		{
			this.Key = k;
			this.Value = v;
		}

		public readonly K Key;

		public readonly V Value;

		public bool WasAccessed
		{
			get => this.wasAccessed;
			set => this.wasAccessed = value;
		}
	}
}
