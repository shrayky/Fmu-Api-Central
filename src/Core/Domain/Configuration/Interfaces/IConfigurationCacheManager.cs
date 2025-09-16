using CSharpFunctionalExtensions;

namespace Domain.Configuration.Interfaces
{
    public interface IConfigurationCacheManager
    {
        Task<Result<Parameters>> GetCachedConfiguration();
        void CacheConfiguration(Parameters parameters);
        void InvalidateCache();
        bool IsConfigurationCached();
    }
}
