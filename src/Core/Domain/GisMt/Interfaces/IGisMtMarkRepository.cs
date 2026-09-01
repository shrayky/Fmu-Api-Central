using CSharpFunctionalExtensions;
using Domain.GisMt.Entity;
using Domain.GisMt.Models;

namespace Domain.GisMt.Interfaces;

/// <summary>
/// Хранение марок остатка ГИС МТ в CouchDB.
/// </summary>
public interface IGisMtMarkRepository
{
    /// <summary>
    /// Возвращает марку остатка по sGTIN (id документа).
    /// </summary>
    Task<GisMtMarkEntity?> Get(string id);

    /// <summary>
    /// Сохраняет одну марку остатка.
    /// </summary>
    Task<bool> Save(GisMtMarkEntity entity);

    /// <summary>
    /// Сохраняет пакет марок остатка.
    /// </summary>
    Task<bool> SaveRange(IEnumerable<GisMtMarkEntity> entities);

    /// <summary>
    /// Меняет признак продажи марки остатка по sGTIN.
    /// </summary>
    Task<Result<GisMtMarkEntity>> ChangeState(string sGtin, bool sold);

    /// <summary>
    /// Возвращает марки для очистки по сроку хранения и невалидному статусу.
    /// </summary>
    Task<List<GisMtMarkEntity>> GetExpiredForCleanup(DateTime olderThanUtc, int limit);

    /// <summary>
    /// Удаляет марку остатка по идентификатору.
    /// </summary>
    Task<bool> Delete(string id);

    /// <summary>
    /// Поиск марок остатка с пагинацией и опциональным отбором по товарной группе.
    /// </summary>
    Task<Result<GisMtMarkSearchResult>> Search(string searchTerm, int page, int pageSize, string? productGroup = null);
}
