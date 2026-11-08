using System;
using System.Collections.Generic;

namespace AMR_Engine
{
    internal readonly record struct BoundedCacheStatistics(int Entries, long KeyCharacters, long Evictions, long UncachedKeys);

    internal sealed class BoundedCache<TKey, TValue> where TKey : notnull
    {
        private readonly object sync = new();
        private readonly Dictionary<TKey, (TValue Value, long Characters)> entries = new();
        private readonly Queue<TKey> insertionOrder = new();
        private readonly int maxEntries;
        private readonly long maxKeyCharacters;
        private long keyCharacters;
        private long evictions;
        private long uncachedKeys;

        public BoundedCache(int maxEntries, long maxKeyCharacters)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxKeyCharacters);
            this.maxEntries = maxEntries;
            this.maxKeyCharacters = maxKeyCharacters;
        }

        public TValue GetOrAdd(TKey key, long characters, Func<TValue> createValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(characters);
            lock (sync)
            {
                if (entries.TryGetValue(key, out var entry))
                    return entry.Value;

                TValue value = createValue();
                if (characters > maxKeyCharacters)
                {
                    uncachedKeys++;
                    return value;
                }

                // FIFO eviction does not retain an access history or change exact lookup keys.
                while (entries.Count >= maxEntries || keyCharacters > maxKeyCharacters - characters)
                {
                    TKey oldest = insertionOrder.Dequeue();
                    keyCharacters -= entries[oldest].Characters;
                    entries.Remove(oldest);
                    evictions++;
                }
                entries.Add(key, (value, characters));
                insertionOrder.Enqueue(key);
                keyCharacters += characters;
                return value;
            }
        }

        public BoundedCacheStatistics Statistics
        {
            get
            {
                lock (sync)
                    return new(entries.Count, keyCharacters, evictions, uncachedKeys);
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                entries.Clear();
                insertionOrder.Clear();
                keyCharacters = 0;
                evictions = 0;
                uncachedKeys = 0;
            }
        }
    }
}
