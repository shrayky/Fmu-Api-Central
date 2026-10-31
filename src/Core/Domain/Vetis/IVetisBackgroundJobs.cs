using CSharpFunctionalExtensions;

namespace Domain.Vetis;

public interface IVetisIncomingLoader
{
    Task LoadAsync(CancellationToken cancellationToken);
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
