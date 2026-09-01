using Domain.GisMt.Entity;
using Domain.GisMt.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CouchDb.Repositories;

/// <summary>
/// Репозиторий входящих документов ГИС МТ.
/// </summary>
public class GisMtDocumentRepository : BaseCouchDbRepository<GisMtDocumentEntity>, IGisMtDocumentRepository
{
    public GisMtDocumentRepository(IServiceProvider services) : base(
        services.GetRequiredService<Context>().GisMtDocuments, services)
    {
    }

    /// <summary>
    /// Возвращает документ ГИС МТ по идентификатору.
    /// </summary>
    public async Task<GisMtDocumentEntity?> Get(string id)
    {
        if (!_appState.DbState())
            return null;

        return await GetByIdAsync(id);
    }

    /// <summary>
    /// Проверяет, был ли документ уже загружен.
    /// </summary>
    public async Task<bool> Exists(string id)
    {
        if (!_appState.DbState())
            return false;

        var entity = await GetByIdAsync(id);
        return entity != null && !string.IsNullOrEmpty(entity.Id);
    }

    /// <summary>
    /// Сохраняет факт загрузки документа.
    /// </summary>
    public async Task<bool> Save(GisMtDocumentEntity entity)
    {
        if (!_appState.DbState())
            return false;

        if (string.IsNullOrEmpty(entity.Id))
            entity.Id = entity.Number;

        var existing = await GetByIdAsync(entity.Id);
        if (existing == null)
            return await CreateAsync(entity);

        return await UpdateAsync(entity.Id, entity);
    }
}
