using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Delauney.Triangulation
{
    /// <summary>
    /// An insertion-ordered set: O(1) contains/add/remove backed by a dictionary,
    /// with stable enumeration order preserved by a linked list.
    /// Used internally by <see cref="FaceList"/> to maintain the active face queue
    /// in a deterministic order without duplicates.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    public class OrderedSet<T> : ICollection<T>
    {
        protected readonly IDictionary<T, LinkedListNode<T>> m_Dictionary;
        protected readonly LinkedList<T> m_LinkedList;

        /// <summary>Initialises with the default equality comparer.</summary>
        public OrderedSet()
            : this(EqualityComparer<T>.Default)
        {
        }

        /// <summary>Initialises with a custom equality comparer.</summary>
        /// <param name="comparer">Comparer used for membership tests and hashing.</param>
        public OrderedSet(IEqualityComparer<T> comparer)
        {
            m_Dictionary = new Dictionary<T, LinkedListNode<T>>(comparer);
            m_LinkedList = new LinkedList<T>();
        }

        /// <inheritdoc/>
        public int Count => m_Dictionary.Count;

        /// <inheritdoc/>
        public virtual bool IsReadOnly => m_Dictionary.IsReadOnly;

        /// <summary>Removes the element that was inserted first (FIFO peek-and-remove).</summary>
        public void RemoveFirst()
        {
            var m = m_LinkedList.FirstOrDefault();
            if (m != null)
            {
                m_Dictionary.Remove(m);
                m_LinkedList.RemoveFirst();
            }
        }

        void ICollection<T>.Add(T item) => Add(item);

        /// <summary>
        /// Adds <paramref name="item"/> if not already present.
        /// </summary>
        /// <returns><c>true</c> if the item was newly added; <c>false</c> if it was already in the set.</returns>
        public bool Add(T item)
        {
            if (m_Dictionary.ContainsKey(item)) return false;
            var node = m_LinkedList.AddLast(item);
            m_Dictionary.Add(item, node);
            return true;
        }

        /// <summary>Removes all elements.</summary>
        public void Clear()
        {
            m_LinkedList.Clear();
            m_Dictionary.Clear();
        }

        /// <summary>Removes <paramref name="item"/>. Returns <c>false</c> if not found.</summary>
        public bool Remove(T item)
        {
            if (item == null) return false;
            var found = m_Dictionary.TryGetValue(item, out var node);
            if (!found) return false;
            m_Dictionary.Remove(item);
            m_LinkedList.Remove(node);
            return true;
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() => m_LinkedList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public bool Contains(T item) => item != null && m_Dictionary.ContainsKey(item);

        /// <inheritdoc/>
        public void CopyTo(T[] array, int arrayIndex) => m_LinkedList.CopyTo(array, arrayIndex);
    }
}
