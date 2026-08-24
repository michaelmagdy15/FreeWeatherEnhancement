using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SkyWeave.App.Models;

public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache = new();
    private readonly LinkedList<CacheItem> _lruList = new();
    private readonly object _lock = new();

    public LruCache(int capacity)
    {
        _capacity = capacity;
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }
    }

    public bool TryAdd(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_cache.ContainsKey(key))
            {
                return false;
            }

            if (_cache.Count >= _capacity)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _cache.Remove(lastNode.Value.Key);
                    _lruList.RemoveLast();
                    if (lastNode.Value.Value is System.IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            var cacheItem = new CacheItem(key, value);
            var newNode = new LinkedListNode<CacheItem>(cacheItem);
            _lruList.AddFirst(newNode);
            _cache.Add(key, newNode);
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var item in _lruList)
            {
                if (item.Value is System.IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _lruList.Clear();
            _cache.Clear();
        }
    }

    private class CacheItem
    {
        public TKey Key { get; }
        public TValue Value { get; }

        public CacheItem(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }
}
