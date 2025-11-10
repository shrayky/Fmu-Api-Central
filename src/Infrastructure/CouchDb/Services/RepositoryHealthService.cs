using CouchDb.Repositories;
using Domain.Attributes;
using Domain.Database.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CouchDb.Services
{
    [AutoRegisterService(ServiceLifetime.Scoped)]
    public class RepositoryHealthService : IRepositoryHealthService
    {
        private readonly IDbHealthService _dbHealthService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RepositoryHealthService> _logger;

        public RepositoryHealthService(IDbHealthService dbHealthService, IServiceProvider serviceProvider, ILogger<RepositoryHealthService> logger)
        {
            _dbHealthService = dbHealthService;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<bool> HasRecords(string repositoryName)
        {
            var isHealthy = await IsRepositoryHealthy(repositoryName);
            if (!isHealthy)
                return false;

            var count = await GetRecordCount(repositoryName);

            return count > 0;
        }

        public async Task<bool> IsRepositoryHealthy(string repositoryName)
        {
            var dbOnline = await _dbHealthService.IsDatabaseEnabled();
            if (!dbOnline) return false;

            return await _dbHealthService.IsDatabaseAccessible(repositoryName);
        }

        public async Task<int> GetRecordCount(string repositoryName)
        {
            try
            {
                var repository = GetRepositoryByName(repositoryName);
                return await repository.RecordCount();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось получить количество записей для {RepositoryName}", repositoryName);
                return 0;
            }
        }

        private IBaseRepository GetRepositoryByName(string repositoryName)
        {
            return repositoryName switch
            {
                DatabaseSchema.Users => _serviceProvider.GetRequiredService<UsersRepository>(),
                _ => throw new ArgumentException($"Неизвестный репозиторий: {repositoryName}")
            };
        }

        public async Task<Dictionary<string, int>> GetAllRepositoriesRecordCount()
        {
            var repositories = DatabaseSchema.All();
            var counts = new Dictionary<string, int>();

            foreach (var repoName in repositories)
            {
                counts[repoName] = await GetRecordCount(repoName);
            }

            return counts;
        }

    }
}
