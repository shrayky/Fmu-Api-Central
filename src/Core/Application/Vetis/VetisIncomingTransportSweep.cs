using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisIncomingTransportSweep : IVetisIncomingTransportSweep
{
    public const int PageSize = 200;

    private readonly IVetisIncomingRepository _incoming;
    private readonly ILogger<VetisIncomingTransportSweep> _logger;

    public VetisIncomingTransportSweep(IVetisIncomingRepository incoming, ILogger<VetisIncomingTransportSweep> logger)
    {
        _incoming = incoming;
        _logger = logger;
    }

    /// <summary>
    /// Удаление необратимо, поэтому страница оставляет след в журнале: сколько ВСД снято и сколько документов удалено.
    /// </summary>
    public async Task<Result<VetisScanStep>> SweepBatchAsync(string afterId, CancellationToken cancellationToken)
    {
        var page = await _incoming.HeadsAfter(afterId ?? "", PageSize);
        if (page.IsFailure)
            return Result.Failure<VetisScanStep>(page.Error);

        var swept = 0;
        var deleted = 0;
        foreach (var head in page.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = await _incoming.Find(head.Id);
            if (found.IsFailure)
                return Result.Failure<VetisScanStep>(found.Error);

            var document = found.Value;
            if (document == null)
                continue;

            var before = document.Vsds.Count;
            if (!document.DropForeignTransport())
                continue;

            swept += before - document.Vsds.Count;
            if (document.Vsds.Count == 0)
            {
                var removed = await _incoming.Delete(document.Id);
                if (removed.IsFailure)
                    return Result.Failure<VetisScanStep>(removed.Error);

                deleted++;
                continue;
            }

            var saved = await _incoming.Save(document);
            if (saved.IsFailure)
                return Result.Failure<VetisScanStep>(saved.Error);
        }

        var last = page.Value.Count == 0 ? afterId ?? "" : page.Value[^1].Id;
        if (swept > 0 || deleted > 0)
            _logger.LogInformation(
                "Снято чужих TRANSPORT: {Swept}, удалено документов: {Deleted}, страница до {AfterId}",
                swept,
                deleted,
                last);

        return Result.Success(new VetisScanStep(page.Value.Count < PageSize, last));
    }
}
