using CSharpFunctionalExtensions;

namespace Domain.Vetis;

public interface IVetisIncomingLoader
{
    /// <summary>
    /// Сколько разных входящих документов записано за проход.
    /// </summary>
    Task<int> LoadAsync(CancellationToken cancellationToken);
}

public interface IVetisApplicationCleaner
{
    Task<int> CleanAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Шаг обхода журнала: после страницы курсор сдвигается на последний id, короткая страница означает конец.
/// </summary>
public readonly record struct VetisScanStep(bool Finished, string AfterId);

public interface IVetisIncomingSortAligner
{
    Task<Result<VetisScanStep>> AlignBatchAsync(string afterId, CancellationToken cancellationToken);
}

public interface IVetisIncomingTransportSweep
{
    Task<Result<VetisScanStep>> SweepBatchAsync(string afterId, CancellationToken cancellationToken);
}

public interface IVetisIncomingAutoQuencher
{
    Task<VetisQuenchBatch> QuenchBatchAsync(VetisQuenchCursor? cursor, CancellationToken cancellationToken);
}

/// <summary>
/// Error заполнен при обрыве запуска и при отказе гашения. QuenchedDocuments при этом уже погашенные документы.
/// Finished — журнал дочитан, следующий круг начинает сначала.
/// </summary>
public sealed class VetisQuenchBatch
{
    public int QuenchedDocuments { get; init; }

    public string Error { get; init; } = "";

    public bool Finished { get; init; }

    public VetisQuenchCursor Cursor { get; init; } = new();
}

public sealed class VetisQuenchCursor
{
    public List<VetisQuenchLaneMark> Lanes { get; init; } = [];
}

public sealed class VetisQuenchLaneMark
{
    public string OrganizationId { get; init; } = "";

    public string Status { get; init; } = "";

    public bool Dated { get; init; }

    public long AfterSortAt { get; init; }

    public string AfterId { get; init; } = "";

    public bool Exhausted { get; init; }
}
