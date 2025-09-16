namespace Domain.Cache.Interfaces
{
    public interface IMemoryCacheService
    {
        T Get<T>(string key);
        void Set<T>(string key, T value);
        void Set<T>(string key, T value, TimeSpan expire);
        void Remove(string key);
        bool TryGet<T>(string key, out T value);
    }
}
