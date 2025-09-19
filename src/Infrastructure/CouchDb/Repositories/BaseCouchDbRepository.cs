using CouchDb.Dto;
using CouchDB.Driver;
using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;
using Domain.Database.Interfaces;
using Domain.Dto.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CouchDb.Repositories
{
    public class BaseCouchDbRepository<T> : IBaseRepository where T : class, IHaveStringId
    {
        private readonly ILogger<T> _logger;
        protected readonly ICouchDatabase<UniversalDocument<T>> _database;
        protected readonly IParametersService _parameters;
        protected readonly IApplicationState _appState;
        
        protected const string DatabaseUnavailable = "БД недоступна сейчас";

        protected BaseCouchDbRepository(ICouchDatabase<UniversalDocument<T>> database, IServiceProvider services)
        {
            _database = database;
            _logger = services.GetRequiredService<ILogger<T>>();
            _parameters = services.GetRequiredService<IParametersService>();
            _appState = services.GetRequiredService<IApplicationState>();
        }

        public async Task<bool> DatabaseOnLine()
        {
            var configuration = await _parameters.Current();

            return configuration.DatabaseConnection.Enable;
        }

        public async Task<T?> GetByIdAsync(string id)
        {
            var doc = await _database.FindAsync(id);

            return doc?.ToDomain();
        }

        public async Task<bool> CreateAsync(T entity)
        {
            if (string.IsNullOrEmpty(entity.Id))
            {
                entity.Id = Guid.NewGuid().ToString();
            }

            return await SaveDocumentAsync(entity);
        }

        public async Task<bool> UpdateAsync(string id, T entity)
        {
            entity.Id = id;
            return await SaveDocumentAsync(entity);
        }

        private async Task<bool> SaveDocumentAsync(T entity)
        {
            var existingDoc = await _database.FindAsync(entity.Id);
            var doc = UniversalDocument<T>.FromDomain(entity, entity.Id);

            if (existingDoc != null)
            {
                doc.Rev = existingDoc.Rev;
            }

            await _database.AddOrUpdateAsync(doc);
            return true;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var doc = await _database.FindAsync(id);

            if (doc == null)
                return true;

            if (doc.Id == "")
                return false;

            await _database.RemoveAsync(doc);

            return true;
        }

        public async Task<bool> CreateBulkAsync(IEnumerable<T> entities)
        {
            var configuration = await _parameters.Current();
            int BATCH_SIZE = configuration.DatabaseConnection.BulkBatchSize;
            int MAX_PARALLEL_TASKS = configuration.DatabaseConnection.BulkParallelTasks;

            var ids = entities.Select(e => e.Id).ToList();
            var existingDocs = await _database.FindManyAsync(ids);

            var documentBatches = entities
               .Join(
                   existingDocs,
                   entity => entity.Id,
                   doc => doc.Id,
                   (entity, existingDoc) =>
                   {
                       var doc = UniversalDocument<T>.FromDomain(entity, entity.Id);
                       doc.Rev = existingDoc.Rev;
                       return doc;
                   })
               .Union(entities
                   .Where(entity => !existingDocs.Any(doc => doc.Id == entity.Id))
                   .Select(entity => UniversalDocument<T>.FromDomain(entity, entity.Id)))
               .GroupBy(e => e.Id)
               .Select(g => g.Last())
               .Chunk(BATCH_SIZE);

            var dbName = typeof(T).Name.ToLower();

            _logger.LogInformation("Начинаю массовое добавление в {Database}: {Count} документов", dbName, entities.Count());

            using var semaphore = new SemaphoreSlim(MAX_PARALLEL_TASKS);

            var tasks = documentBatches.Select(async batch =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await _database.AddOrUpdateRangeAsync(batch);
                    await Task.Delay(100);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            return true;
        }

        public async Task<List<T>> GetListByIdAsync(List<string> ids)
        {
            var docs = await _database.FindManyAsync(ids);

            List<T> entityData = [];

            foreach (UniversalDocument<T> couchDoc in docs)
            {
                entityData.Add(couchDoc.Data);
            }

            return entityData;
        }

        public async Task<int> RecordCount()
        {
            var info = await _database.GetInfoAsync();

            return info.DocCount;
        }

        public async Task<bool> IsHealthy()
        {
            try
            {
                var info = await _database.GetInfoAsync();
                return info != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
