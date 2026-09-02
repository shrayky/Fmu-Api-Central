using CouchDb.Dto;
using CouchDB.Driver;
using CouchDB.Driver.Types;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Configuration.Interfaces;
using Domain.Database.Interfaces;
using Domain.Entitys.Interfaces;
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
            var response = await _database.ReadItemAsync(id);
            return response?.Document.ToDomain();
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

        public async Task<bool> DeleteAsync(string id)
        {
            var response = await _database.ReadItemAsync(id);

            if (response == null)
                return true;

            var doc = response.Document;
            if (doc.Id == "")
                return false;

            var rev = string.IsNullOrEmpty(doc.Rev) ? response.Rev : doc.Rev;
            await _database.DeleteItemAsync(doc.Id, rev);

            return true;
        }

        public async Task<bool> CreateBulkAsync(IEnumerable<T> entities)
        {
            var configuration = await _parameters.Current();
            int BATCH_SIZE = configuration.DatabaseConnection.BulkBatchSize;
            int MAX_PARALLEL_TASKS = configuration.DatabaseConnection.BulkParallelTasks;

            var entityList = entities
                .GroupBy(e => e.Id)
                .Select(g => g.Last())
                .ToList();
            var ids = entityList.Select(e => e.Id).ToList();
            var existingDocs = await _database.ReadItemsAsync(ids);
            var existingById = existingDocs
                .GroupBy(doc => doc.Id)
                .ToDictionary(g => g.Key, g => g.Last());

            var documentBatches = entityList
                .Select(entity =>
                {
                    var doc = UniversalDocument<T>.FromDomain(entity, entity.Id);
                    if (existingById.TryGetValue(entity.Id, out var existingDoc))
                        doc.Rev = existingDoc.Rev;
                    return doc;
                })
                .Chunk(BATCH_SIZE);

            var dbName = typeof(T).Name.ToLower();

            _logger.LogInformation("Начинаю массовое добавление в {Database}: {Count} документов", dbName, entityList.Count);

            using var semaphore = new SemaphoreSlim(MAX_PARALLEL_TASKS);

            var tasks = documentBatches.Select(async batch =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var operations = batch
                        .Select(doc => string.IsNullOrEmpty(doc.Rev)
                            ? BulkItemOperation.Add(doc)
                            : BulkItemOperation.Update(doc, doc.Id, doc.Rev))
                        .ToList();

                    await _database.ExecuteBulkItemOperationsAsync(operations);
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
            var docs = await _database.ReadItemsAsync(ids);

            List<T> entityData = [];

            foreach (UniversalDocument<T> couchDoc in docs)
            {
                entityData.Add(couchDoc.Data);
            }

            return entityData;
        }

        /// <summary>
        /// Количество документов без design-документов индексов.
        /// </summary>
        public async Task<int> RecordCount()
        {
            var info = await _database.GetInfoAsync();
            var indexes = await _database.GetIndexesAsync();
            var designDocCount = indexes
                .Where(index => !string.IsNullOrWhiteSpace(index.DesignDocument))
                .Select(index => index.DesignDocument)
                .Distinct(StringComparer.Ordinal)
                .Count();

            return Math.Max(0, (int)info.DocCount - designDocCount);
        }

        /// <summary>
        /// Выполняет mango-запрос _find и возвращает Data документов.
        /// </summary>
        protected async Task<Result<List<T>>> ExecuteMangoQueryAsync(object mangoQuery)
        {
            try
            {
                var result = await _database.QueryAsync(mangoQuery, throwExceptionOnWarning: false);
                var list = result
                    .Where(doc => doc.Data != null)
                    .Select(doc => doc.Data)
                    .ToList();
                return Result.Success(list);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка mango-запроса");
                return Result.Failure<List<T>>("Ошибка запроса к БД");
            }
        }

        /// <summary>
        /// Сохраняет уже прочитанные документы с текущим Rev.
        /// </summary>
        protected async Task SaveExistingDocumentsAsync(IEnumerable<UniversalDocument<T>> documents)
        {
            var operations = documents
                .Select(doc => BulkItemOperation.Update(doc, doc.Id, doc.Rev))
                .ToList();

            if (operations.Count == 0)
                return;

            await _database.ExecuteBulkItemOperationsAsync(operations);
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

        private async Task<bool> SaveDocumentAsync(T entity)
        {
            var existingResponse = await _database.ReadItemAsync(entity.Id);
            var doc = UniversalDocument<T>.FromDomain(entity, entity.Id);

            if (existingResponse != null)
                await _database.UpdateItemAsync(doc, entity.Id, existingResponse.Rev);
            else
                await _database.CreateItemAsync(doc);

            return true;
        }
    }
}
