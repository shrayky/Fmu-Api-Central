using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<RefreshTokenService> _logger;

        public RefreshTokenService(IMemoryCache cache, ILogger<RefreshTokenService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Result<string> LoginByRefreshToken(string refreshToken)
        {
            string? login;
            
            if (!_cache.TryGetValue(CacheKey(refreshToken), out login) || login == null)
                return Result.Failure<string>("Не найден refresh токен");

            return Result.Success(login);
        }

        public bool IsRefreshTokenValid(string refreshToken)
            => _cache.TryGetValue(CacheKey(refreshToken), out _);
        public void RemoveRefreshToken(string refreshToken)
            => _cache.Remove(CacheKey(refreshToken));
        public void SaveRefreshToken(string refreshToken, string login, DateTime expiresAt)
            => _cache.Set(CacheKey(refreshToken), login, expiresAt);

        private static string CacheKey(string refreshToken) => $"refresh_{refreshToken}";
    }
}
