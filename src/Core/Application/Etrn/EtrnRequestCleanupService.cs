using Domain.Attributes;
using Domain.Etrn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Etrn;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class EtrnRequestCleanupService : IEtrnRequestCleaner
{
    private const int BatchSize = 200;

    private readonly IEtrnRequestRepository _requests;
    private readonly ILogger<EtrnRequestCleanupService> _logger;
    private readonly TimeProvider _clock;

    public EtrnRequestCleanupService(
        IEtrnRequestRepository requests,
        ILogger<EtrnRequestCleanupService> logger,
        TimeProvider? clock = null)
    {
        _requests = requests;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var deleted = await DeleteExpiredAsync(
            true,
            (now - EtrnRequestRetention.Success).ToUnixTimeSeconds(),
            cancellationToken);
        deleted += await DeleteExpiredAsync(
            false,
            (now - EtrnRequestRetention.Failure).ToUnixTimeSeconds(),
            cancellationToken);

        if (deleted > 0)
            _logger.LogInformation("Удалено устаревших запросов ЭТрН: {Count}", deleted);

        return deleted;
    }

    private async Task<int> DeleteExpiredAsync(bool completed, long createdBefore, CancellationToken cancellationToken)
    {
        var deleted = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var expired = await _requests.ListExpired(completed, createdBefore, BatchSize);
            if (expired.IsFailure || expired.Value.Count == 0)
                break;

            var removed = await _requests.Delete(expired.Value.Select(item => item.Id).ToList());
            if (removed.IsFailure)
            {
                _logger.LogWarning("Не удалось удалить устаревшие запросы ЭТрН: {Error}", removed.Error);
                break;
            }

            deleted += expired.Value.Count;
        }

        return deleted;
    }
}
