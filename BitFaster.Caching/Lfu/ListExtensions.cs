using System;
using System.Collections.Generic;
using System.Text;
using BitFaster.Caching.Lru;

namespace BitFaster.Caching.Lfu
{
    public static class ListExtensions
    {
        public static void MoveToEnd<T>(this LinkedList<T> list, LinkedListNode<T> node)
        {
            list.Remove(node);
            list.AddLast(node);
        }
    }
}
