using System.Collections.Concurrent;

namespace SkyWeave.Core.Services;

public class WeatherCache
{
    private readonly ConcurrentDictionary<string, (object Data, DateTime Expires)> _cache = new();

    public bool TryGet<T>(string key, out T? data)
    {
        if (_cache.TryGetValue(key, out var entry) &&
            entry.Expires > DateTime.UtcNow &&
            entry.Data is T cached)
        {
            data = cached;
            return true;
        }

        data = default;
        return false;
    }

    public void Set<T>(string key, T data, TimeSpan ttl)
    {
        var expires = DateTime.UtcNow + ttl;
        _cache[key] = (data!, expires);
    }

    public void Remove(string key)
    {
        _cache.TryRemove(key, out _);
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public async Task<T?> GetOrFetchAsync<T>(
        string key,
        Func<Task<T?>> fetch,
        TimeSpan ttl)
    {
        if (TryGet<T>(key, out var cached))
            return cached;

        var data = await fetch();
        if (data != null)
            Set(key, data, ttl);

        return data;
    }
}
