using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisIncomingSortAligner : IVetisIncomingSortAligner
{
    public const int PageSize = 200;

    private readonly IVetisIncomingRepository _incoming;

    public VetisIncomingSortAligner(IVetisIncomingRepository incoming) => _incoming = incoming;

    public async Task<Result<VetisScanStep>> AlignBatchAsync(string afterId, CancellationToken cancellationToken)
    {
        var page = await _incoming.HeadsAfter(afterId ?? "", PageSize);
        if (page.IsFailure)
            return Result.Failure<VetisScanStep>(page.Error);

        foreach (var head in page.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (head.SortAt == VetisIncomingDocumentEntity.DocumentSortAt(head.Date))
                continue;

            var found = await _incoming.Find(head.Id);
            if (found.IsFailure)
                return Result.Failure<VetisScanStep>(found.Error);
            if (found.Value == null || !found.Value.AlignSortAt())
                continue;

            var saved = await _incoming.Save(found.Value);
            if (saved.IsFailure)
                return Result.Failure<VetisScanStep>(saved.Error);
        }

        var last = page.Value.Count == 0 ? afterId ?? "" : page.Value[^1].Id;
        return Result.Success(new VetisScanStep(page.Value.Count < PageSize, last));
    }
}
