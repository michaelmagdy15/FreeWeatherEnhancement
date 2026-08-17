using System.Net.Http;

namespace SkyWeave.Core.Fetchers;

public static class FetchRetry
{
    public static async Task<T?> WithRetryAsync<T>(Func<Task<T?>> fetch, int maxRetries = 3, TimeSpan? baseDelay = null)
    {
        baseDelay ??= TimeSpan.FromSeconds(1);
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var result = await fetch();
                if (result != null)
                    return result;
            }
            catch (OperationCanceledException)
            {
                if (attempt == maxRetries - 1)
                    return default;
            }
            catch (HttpRequestException)
            {
                if (attempt == maxRetries - 1)
                    return default;
            }
            catch
            {
                if (attempt == maxRetries - 1)
                    return default;
            }

            if (attempt < maxRetries - 1)
                await Task.Delay(baseDelay.Value * Math.Pow(2, attempt));
        }
        return default;
    }
}
