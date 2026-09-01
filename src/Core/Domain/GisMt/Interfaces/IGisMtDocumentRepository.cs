using Domain.GisMt.Entity;

namespace Domain.GisMt.Interfaces;

/// <summary>
/// Хранение входящих документов ГИС МТ в CouchDB.
/// </summary>
public interface IGisMtDocumentRepository
{
    /// <summary>
    /// Возвращает документ ГИС МТ по идентификатору.
    /// </summary>
    Task<GisMtDocumentEntity?> Get(string id);

    /// <summary>
    /// Проверяет, был ли документ уже загружен.
    /// </summary>
    Task<bool> Exists(string id);

    /// <summary>
    /// Сохраняет факт загрузки документа.
    /// </summary>
    Task<bool> Save(GisMtDocumentEntity entity);
}
