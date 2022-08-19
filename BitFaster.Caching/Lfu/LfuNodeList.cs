using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

namespace BitFaster.Caching.Lfu
{
    /// <summary>
    /// Adapted from the .NET linked list code, but with arg checking only applied in debug builds. 
    /// </summary>
    [DebuggerDisplay("Count = {Count}")]
    internal class LfuNodeList<K, V> : IEnumerable<LfuNode<K, V>>
    {
        internal LfuNode<K, V> head;
        private int count;

        public int Count
        {
            get { return count; }
        }

        public LfuNode<K, V> First
        {
            get { return head; }
        }

        public LfuNode<K, V> Last
        {
            get { return head?.prev; }
        }

        public void MoveToEnd(LfuNode<K, V> node)
        {
            this.Remove(node);
            this.AddLast(node);
        }

        public void AddLast(LfuNode<K, V> node)
        {
#if DEBUG
            ValidateNewNode(node);
#endif

            if (head == null)
            {
                InternalInsertNodeToEmptyList(node);
            }
            else
            {
                InternalInsertNodeBefore(head, node);
            }

            node.list = this;
        }

        public void Remove(LfuNode<K, V> node)
        {
#if DEBUG
            ValidateNode(node);
#endif
            InternalRemoveNode(node);
        }

        public void RemoveFirst()
        {
#if DEBUG
            if (head == null) { throw new InvalidOperationException("List is empty."); }
#endif
            InternalRemoveNode(head);
        }

        public void Clear()
        {
            LfuNode<K, V> current = head;

            while (current != null)
            {
                LfuNode<K, V> temp = current;
                current = current.Next;
                temp.Invalidate();
            }

            head = null;
            count = 0;
        }

        private void InternalInsertNodeToEmptyList(LfuNode<K, V> newNode)
        {
            newNode.next = newNode;
            newNode.prev = newNode;
            head = newNode;
            count++;
        }

        private void InternalInsertNodeBefore(LfuNode<K, V> node, LfuNode<K, V> newNode)
        {
            newNode.next = node;
            newNode.prev = node.prev;
            node.prev.next = newNode;
            node.prev = newNode;
            count++;
        }

        internal void InternalRemoveNode(LfuNode<K, V> node)
        {
            if (node.next == node)
            {
                head = null;
            }
            else
            {
                node.next.prev = node.prev;
                node.prev.next = node.next;
                if (head == node)
                {
                    head = node.next;
                }
            }

            node.Invalidate();
            count--;
        }

        public IEnumerator<LfuNode<K, V>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

#if DEBUG
        internal static void ValidateNewNode(LfuNode<K, V> node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.list != null)
            {
                throw new InvalidOperationException("Node is already attached to a list.");
            }
        }

        internal void ValidateNode(LfuNode<K, V> node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.list != this)
            {
                throw new InvalidOperationException("Node is already attached to a different list.");
            }
        }
#endif

        public struct Enumerator : IEnumerator<LfuNode<K, V>>, IEnumerator
        {
            private readonly LfuNodeList<K, V> list;
            private LfuNode<K, V> node;
            private LfuNode<K, V> current;
            private int index;

            internal Enumerator(LfuNodeList<K, V> list)
            {
                this.list = list;
                node = list.head;
                current = default;
                index = 0;
            }

            public LfuNode<K, V> Current => current;

            object IEnumerator.Current
            {
                get
                {
                    if (index == 0 || (index == list.Count + 1))
                    {
                        throw new InvalidOperationException("Out of bounds");
                    }

                    return Current;
                }
            }

            public bool MoveNext()
            {
                if (node == null)
                {
                    index = list.Count + 1;
                    return false;
                }

                ++index;
                current = node;
                node = node.next;

                if (node == list.head)
                {
                    node = null;
                }
                
                return true;
            }

            void IEnumerator.Reset()
            {
                current = default;
                node = list.head;
                index = 0;
            }

            public void Dispose()
            {
            }
        }
    }
}
