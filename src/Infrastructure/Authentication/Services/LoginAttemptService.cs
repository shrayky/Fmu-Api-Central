using Domain.Attributes;
using Domain.Authentication;
using Domain.Authentication.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Authentication.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class LoginAttemptService : ILoginAttemptService
{
    private const string CacheKeyPrefix = "login_attempt_";

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly LoginAttemptOptions _options;

    public LoginAttemptService(IMemoryCache cache, TimeProvider timeProvider, LoginAttemptOptions options)
    {
        _cache = cache;
        _timeProvider = timeProvider;
        _options = options;
    }

    public bool IsLocked(string login)
    {
        var state = GetState(login);
        if (state?.LockedUntil == null)
            return false;

        return state.LockedUntil > _timeProvider.GetUtcNow();
    }

    public void RegisterFailure(string login)
    {
        var key = CacheKey(login);
        var now = _timeProvider.GetUtcNow();
        var state = GetState(login) ?? new LoginAttemptState();

        if (state.LockedUntil > now)
            return;

        if (state.LockedUntil != null && state.LockedUntil <= now)
        {
            state.FailedCount = 0;
            state.LockedUntil = null;
        }

        state.FailedCount++;

        if (state.FailedCount >= _options.MaxFailedAttempts)
            state.LockedUntil = now.Add(_options.LockoutDuration);

        _cache.Set(key, state, now.Add(_options.LockoutDuration));
    }

    public void RegisterSuccess(string login)
        => _cache.Remove(CacheKey(login));

    private LoginAttemptState? GetState(string login)
    {
        _cache.TryGetValue(CacheKey(login), out LoginAttemptState? state);
        return state;
    }

    private static string CacheKey(string login)
        => $"{CacheKeyPrefix}{(login ?? string.Empty).Trim().ToLowerInvariant()}";

    private sealed class LoginAttemptState
    {
        public int FailedCount { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
