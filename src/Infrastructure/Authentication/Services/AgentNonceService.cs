using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication;
using Domain.Authentication.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Authentication.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class AgentNonceService : IAgentNonceService
{
    private const string CacheKeyPrefix = "agent_nonce_";

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly AgentNonceOptions _options;

    public AgentNonceService(IMemoryCache cache, TimeProvider timeProvider, AgentNonceOptions options)
    {
        _cache = cache;
        _timeProvider = timeProvider;
        _options = options;
    }

    public Result Consume(string nonce, long unixTimestamp)
    {
        if (string.IsNullOrWhiteSpace(nonce))
            return Result.Failure("Пустой nonce");

        var now = _timeProvider.GetUtcNow();
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);

        if (Abs(now - timestamp) > _options.TimestampWindow)
            return Result.Failure("Метка времени отклонена");

        var key = $"{CacheKeyPrefix}{nonce.Trim()}";
        if (_cache.TryGetValue(key, out _))
            return Result.Failure("Повторный nonce");

        _cache.Set(key, true, _options.NonceLifetime);
        return Result.Success();
    }

    private static TimeSpan Abs(TimeSpan value) => value < TimeSpan.Zero ? -value : value;
}
