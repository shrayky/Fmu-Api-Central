using CSharpFunctionalExtensions;
using Domain.Vetis;

namespace CouchDb.Repositories;

public static class VetisIncomingRelease
{
    /// <summary>
    /// Сначала все id: Drop меняет sortAt, а удаление сдвигает хвост, и следующий skip такой документ уже не видит.
    /// </summary>
    public static async Task<Result> Apply(
        Func<int, int, Task<Result<List<VetisIncomingDocumentEntity>>>> readPage,
        IReadOnlyCollection<string> uuids,
        IReadOnlyCollection<string> keepDocumentIds,
        Func<string, Task<Result<VetisIncomingDocumentEntity?>>> find,
        Func<VetisIncomingDocumentEntity, Task<Result>> save,
        Func<string, Task<Result>> delete,
        int pageSize)
    {
        var ids = new List<string>();
        var skip = 0;
        while (true)
        {
            var page = await readPage(skip, pageSize);
            if (page.IsFailure)
                return Result.Failure(page.Error);

            ids.AddRange(page.Value.Select(document => document.Id));
            if (page.Value.Count < pageSize)
                break;

            skip += page.Value.Count;
        }

        var keep = new HashSet<string>(keepDocumentIds, StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            if (keep.Contains(id))
                continue;

            var loaded = await find(id);
            if (loaded.IsFailure)
                return Result.Failure(loaded.Error);
            if (loaded.Value == null || !loaded.Value.Drop(uuids))
                continue;

            var stored = loaded.Value.Vsds.Count == 0
                ? await delete(loaded.Value.Id)
                : await save(loaded.Value);
            if (stored.IsFailure)
                return stored;
        }

        return Result.Success();
    }
}
