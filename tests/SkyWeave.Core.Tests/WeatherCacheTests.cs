using SkyWeave.Core.Services;
using Xunit;
using System.Diagnostics;

namespace SkyWeave.Core.Tests;

public class WeatherCacheTests
{
    [Fact]
    public async Task GetOrFetchAsync_PreventsConcurrentCacheStampedes()
    {
        var cache = new WeatherCache();
        var fetchCount = 0;

        async Task<string?> FetchFuncAsync()
        {
            Interlocked.Increment(ref fetchCount);
            await Task.Delay(100);
            return "DATA";
        }

        var tasks = new List<Task<string?>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(cache.GetOrFetchAsync("TEST_KEY", FetchFuncAsync, TimeSpan.FromMinutes(10)));
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, fetchCount); // Ensures only one fetch was executed
        Assert.All(results, r => Assert.Equal("DATA", r));
    }
}
