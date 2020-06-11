using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lightweight.Caching.Lru
{
	public enum ItemDestination
	{
		Warm,
		Cold,
		Remove
	}	
}
