using StackExchange.Redis;

namespace Restaurante.Api.Services;

public class CacheService(IConfiguration configuration)
{
    private readonly Lazy<ConnectionMultiplexer> _redis = new(() =>
        ConnectionMultiplexer.Connect(configuration["Redis"] ?? "localhost:6379"));

    public async Task SetAsync(string key, string value, TimeSpan expiry)
    {
        await _redis.Value.GetDatabase().StringSetAsync(key, value, expiry);
    }

    public async Task<string?> GetAsync(string key)
    {
        var value = await _redis.Value.GetDatabase().StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }
}
