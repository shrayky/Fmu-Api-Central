using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Configuration.Services
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class ConfigurationCacheManager : IConfigurationCacheManager
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<ConfigurationCacheManager> _logger;

        private const string CacheKey = "app_settings";
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(600);

        public ConfigurationCacheManager(IMemoryCache memoryCache, ILogger<ConfigurationCacheManager> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<Result<Parameters>> GetCachedConfiguration()
        {
            var cachedConfig = await _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;

                // Возвращаем null, чтобы показать, что кэш пуст
                // Реальная загрузка будет происходить в вызывающем коде
                return (Parameters?)null;
            });

            if (cachedConfig != null)
                return Result.Success(cachedConfig);

            return Result.Failure<Parameters>("Конфигурация не найдена в кэше");
        }

        public void CacheConfiguration(Parameters parameters)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheExpiration
            };

            _memoryCache.Set(CacheKey, parameters, options);
        }

        public void InvalidateCache()
        {
            _memoryCache.Remove(CacheKey);
            _logger.LogDebug("Кэш конфигурации очищен");
        }

        public bool IsConfigurationCached()
        {
            return _memoryCache.TryGetValue(CacheKey, out _);
        }
    }
}
