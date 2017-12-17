﻿using ICU4N.Support.Collections;
using ICU4N.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ICU4N.Impl
{
    /// <summary>
    /// Convenience Methods
    /// </summary>
    public static class Relation
    {
        public static Relation<TKey, TValue> Of<TKey, TValue>(IDictionary<TKey, ISet<TValue>> map, Type setCreator)
            where TValue : class
        {
            return new Relation<TKey, TValue>(map, setCreator);
        }
        public static Relation<TKey, TValue> Of<TKey, TValue>(IDictionary<TKey, ISet<TValue>> map, Type setCreator, IComparer<TValue> setComparer)
            where TValue : class
        {
            return new Relation<TKey, TValue>(map, setCreator, setComparer);
        }
    }

    /// <summary>
    /// A Relation is a set of mappings from keys to values.
    /// Unlike <see cref="IDictionary{TKey, TValue}"/>, there is not guaranteed to be a single value per key.
    /// The dictionary-like APIs return collections for values.
    /// </summary>
    /// <typeparam name="TKey">Type of key.</typeparam>
    /// <typeparam name="TValue">Type of value.</typeparam>
    /// <author>medavis</author>
    public class Relation<TKey, TValue> : IFreezable<Relation<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>
        where TValue : class // ICU4N specific - using only reference types so we don't have to change from null to default(TValue)
    {
        private IDictionary<TKey, ISet<TValue>> data;

        ConstructorInfo setCreator;
        object[] setComparerParam;

        public Relation(IDictionary<TKey, ISet<TValue>> map, Type setCreator)
            : this(map, setCreator, null)
        {
        }

        public Relation(IDictionary<TKey, ISet<TValue>> map, Type setCreator, IComparer<TValue> setComparator)
        {
            try
            {
                setComparerParam = setComparator == null ? null : new object[] { setComparator };
                if (setComparator == null)
                {
                    this.setCreator = ((Type)setCreator).GetConstructor(new Type[0]);
                    this.setCreator.Invoke(setComparerParam); // check to make sure compiles
                }
                else
                {
                    this.setCreator = ((Type)setCreator).GetConstructor(new Type[] { typeof(IComparer<TValue>) });
                    this.setCreator.Invoke(setComparerParam); // check to make sure compiles
                }
                data = map == null ? new Dictionary<TKey, ISet<TValue>>() : map;
            }
            catch (Exception e)
            {
                throw new ArgumentException("Can't create new set", e);
            }
        }

        public virtual void Clear()
        {
            data.Clear();
        }

        public virtual bool ContainsKey(TKey key)
        {
            return data.ContainsKey(key);
        }

        public virtual bool ContainsValue(TValue value)
        {
            foreach (var values in data.Values)
            {
                if (values.Contains(value))
                {
                    return true;
                }
            }
            return false;
        }

        // ICU4N specific - replaced entrySet() and keyValueSet() by implementing IEnumerable<KeyValuePair<TKey, TValue>,
        // to be closer to how dictionaries are implemented in .NET

        public ICollection<KeyValuePair<TKey, ISet<TValue>>> KeyValues
        {
            get { return data; }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        private class Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly Relation<TKey, TValue> outerInstance;
            private readonly IEnumerator<KeyValuePair<TKey, ISet<TValue>>> dataEnumerator;
            private TKey key;
            private IEnumerator<TValue> setEnumerator;
            private KeyValuePair<TKey, TValue> current;
            
            public Enumerator(Relation<TKey, TValue> outerInstance)
            {
                this.outerInstance = outerInstance;
                this.dataEnumerator = outerInstance.data.GetEnumerator();
            }

            public KeyValuePair<TKey, TValue> Current => current;

            object IEnumerator.Current => current;

            public void Dispose()
            {
                this.setEnumerator.Dispose();
            }

            public bool MoveNext()
            {
                if (setEnumerator == null)
                {
                    if (!MoveNextData())
                        return false;
                }

                bool setHasNext = setEnumerator.MoveNext();
                if (!setHasNext)
                {
                    if (!MoveNextData())
                        return false;

                    if (!setEnumerator.MoveNext())
                        return false;
                }

                current = new KeyValuePair<TKey, TValue>(key, setEnumerator.Current);
                return true;
            }

            private bool MoveNextData()
            {
                bool hasNext = dataEnumerator.MoveNext();
                if (hasNext)
                {
                    var currentData = dataEnumerator.Current;
                    key = currentData.Key;
                    setEnumerator = currentData.Value.GetEnumerator();
                }
                return hasNext;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }
        }

        public override bool Equals(object o)
        {
            if (o == null)
                return false;
            if (o.GetType() != this.GetType())
                return false;
            return data.Equals(((Relation<TKey, TValue>)o).data);
        }

        //  public V get(Object key) {
        //      Set<V> set = data.get(key);
        //      if (set == null || set.size() == 0)
        //        return null;
        //      return set.iterator().next();
        //  }

        public ISet<TValue> GetAll(TKey key)
        {
            return data.Get(key);
        }

        public ISet<TValue> Get(TKey key)
        {
            return data.Get(key);
        }

        public override int GetHashCode()
        {
            return CollectionUtil.GetHashCode(data);
        }

        //public virtual bool IsEmpty // ICU4N specific - removed because this is not .NET-like
        //{
        //    get { return data.Count == 0; }
        //}

        public virtual ICollection<TKey> Keys
        {
            get { return data.Keys; }
        }

        public virtual TValue Put(TKey key, TValue value)
        {
            ISet<TValue> set = data.Get(key);
            if (set == null)
            {
                data[key] = set = NewSet();
            }
            set.Add(value);
            return value;
        }

        public virtual TValue PutAll(TKey key, ICollection<TValue> values)
        {
            ISet<TValue> set = data.Get(key);
            if (set == null)
            {
                data[key] = set = NewSet();
            }
            set.UnionWith(values);
            return values.Count == 0 ? null : values.First();
        }

        public virtual TValue PutAll(ICollection<TKey> keys, TValue value)
        {
            TValue result = null;
            foreach (var key in keys)
            {
                result = Put(key, value);
            }
            return result;
        }

        private ISet<TValue> NewSet()
        {
            try
            {
                return (ISet<TValue>)setCreator.Invoke(setComparerParam);
            }
            catch (Exception e)
            {
                throw new ArgumentException("Can't create new set", e);
            }
        }

        public virtual void PutAll<K, V>(IDictionary<K, V> t)
            where K : TKey
            where V : TValue
        {
            foreach (var entry in t)
            {
                Put(entry.Key, entry.Value);
            }
        }

        public virtual void PutAll<K, V>(Relation<K, V> t)
            where K : TKey
            where V : class, TValue
        {
            foreach (var key in t.Keys)
            {
                foreach (var value in t.GetAll(key))
                {
                    Put(key, value);
                }
            }
        }

        public virtual ISet<TValue> RemoveAll(TKey key)
        {
            try
            {
                if (data.TryGetValue(key, out ISet<TValue> values))
                {
                    data.Remove(key);
                    return values;
                }

                return null;
            }
            catch (NullReferenceException)
            {
                return null; // data doesn't allow null, eg ConcurrentHashMap
            }
        }

        public virtual bool Remove(TKey key, TValue value)
        {
            try
            {
                ISet<TValue> set = data.Get(key);
                if (set == null)
                {
                    return false;
                }
                bool result = set.Remove(value);
                if (set.Count == 0)
                {
                    data.Remove(key);
                }
                return result;
            }
            catch (NullReferenceException)
            {
                return false; // data doesn't allow null, eg ConcurrentHashMap
            }
        }

        public virtual int Count
        {
            get { return data.Count; }
        }

        public virtual ISet<TValue> Values
        {
            get { return GetValues(new HashSet<TValue>()); }
        }

        public virtual C GetValues<C>(C result)
            where C : ICollection<TValue>
        {
            foreach (var keyValue in data)
            {
                foreach (var value in keyValue.Value)
                {
                    result.Add(value);
                }
            }
            return result;
        }

        public override string ToString()
        {
            return CollectionUtil.ToString(data);
        }

        // ICU4N specific - SimpleEntry class not needed, since we have implemented IEnumerator.
        // KeyValuePair is sealed anyway, so this won't work.

        //internal class SimpleEntry /*: KeyValuePair<TKey, TValue>*/
        //{
        //    TKey key;
        //    TValue value;

        //    public SimpleEntry(TKey key, TValue value)
        //    {
        //        this.key = key;
        //        this.value = value;
        //    }

        //    public SimpleEntry(KeyValuePair<TKey, TValue> e)
        //    {
        //        this.key = e.Key;
        //        this.value = e.Value;
        //    }

        //    public virtual TKey Key
        //    {
        //        get { return key; }
        //    }

        //    public virtual TValue Value
        //    {
        //        get { return value; }
        //    }

        //    public virtual TValue SetValue(TValue value) // ICU4N TODO: Perhaps just make an extension method for KeyValuePair, since it is sealed?
        //    {
        //        TValue oldValue = this.value;
        //        this.value = value;
        //        return oldValue;
                
        //    }
        //}

        public virtual Relation<TKey, TValue> AddAllInverted<K>(Relation<TValue, K> source)
                where K : class, TKey
        {
            foreach (TValue value in source.data.Keys)
            {
                foreach (TKey key in source.data.Get(value))
                {
                    Put(key, value);
                }
            }
            return this;
        }

        public virtual Relation<TKey, TValue> AddAllInverted<K>(IDictionary<TValue, K> source)
                where K : class, TKey
        {
            foreach (var entry in source)
            {
                Put(entry.Value, entry.Key);
            }
            return this;
        }

        volatile bool frozen = false;

        public virtual bool IsFrozen
        {
            get { return frozen; }
        }

        public virtual Relation<TKey, TValue> Freeze()
        {
            if (!frozen)
            {
                // does not handle one level down, so we do that on a case-by-case basis
                foreach (var key in data.Keys)
                {
                    data[key] = data.Get(key).ToUnmodifiableSet();
                }
                // now do top level
                data = data.ToUnmodifiableDictionary();
                frozen = true;
            }
            return this;
        }

        public virtual Relation<TKey, TValue> CloneAsThawed()
        {
            // TODO do later
            throw new NotSupportedException();
        }

        public virtual bool RemoveAll(Relation<TKey, TValue> toBeRemoved)
        {
            bool result = false;
            foreach (var key in toBeRemoved.Keys)
            {
                try
                {
                    ISet<TValue> values = toBeRemoved.GetAll(key);
                    if (values != null)
                    {
                        result |= RemoveAll(key, values);
                    }
                }
                catch (NullReferenceException)
                {
                    // data doesn't allow null, eg ConcurrentHashMap
                }
            }
            return result;
        }

        public virtual ISet<TValue> RemoveAll(params TKey[] keys)
        {
            return RemoveAll((ICollection<TKey>)keys);
        }

        public virtual bool RemoveAll(TKey key, IEnumerable<TValue> toBeRemoved)
        {
            bool result = false;
            foreach (var value in toBeRemoved)
            {
                result |= Remove(key, value);
            }
            return result;
        }

        public virtual ISet<TValue> RemoveAll(ICollection<TKey> toBeRemoved)
        {
            ISet<TValue> result = new HashSet<TValue>();
            foreach (var key in toBeRemoved)
            {
                try
                {
                    if (data.TryGetValue(key, out ISet<TValue> removals))
                    {
                        data.Remove(key);
                        result.UnionWith(removals);
                    }
                }
                catch (NullReferenceException)
                {
                    // data doesn't allow null, eg ConcurrentHashMap
                }
            }
            return result;
        }
    }
}
