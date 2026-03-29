using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace FuelFinder.Api.Cache;

static class CacheExtensions
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    internal static async Task<T?> GetJsonAsync<T>(
        this IDistributedCache cache, string key, CancellationToken ct = default)
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, JsonOpts);
        }
        catch { return default; } // Redis unavailable — fall through to DB
    }

    internal static async Task SetJsonAsync<T>(
        this IDistributedCache cache, string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);
            await cache.SetAsync(key, bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch { } // Redis unavailable — cache write is best-effort
    }

    internal static async Task RemoveSafeAsync(
        this IDistributedCache cache, string key, CancellationToken ct = default)
    {
        try { await cache.RemoveAsync(key, ct); }
        catch { }
    }
}
