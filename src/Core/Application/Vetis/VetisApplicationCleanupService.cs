using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisApplicationCleanupService : IVetisApplicationCleaner
{
    private const int BatchSize = 200;

    private readonly IVetisApplicationRepository _applications;
    private readonly IParametersService _parameters;
    private readonly ILogger<VetisApplicationCleanupService> _logger;
    private readonly TimeProvider _clock;

    public VetisApplicationCleanupService(
        IVetisApplicationRepository applications,
        IParametersService parameters,
        ILogger<VetisApplicationCleanupService> logger,
        TimeProvider? clock = null)
    {
        _applications = applications;
        _parameters = parameters;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Незавершённые заявки хранятся дольше выполненных: по ним разбирают, почему Ветис не ответил.
    /// </summary>
    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        var settings = (await _parameters.Current()).VetisSettings ?? new();
        var now = _clock.GetUtcNow();
        var deleted = await DeleteExpiredAsync(true, (now - settings.CompletedRetention()).ToUnixTimeSeconds(), cancellationToken);
        deleted += await DeleteExpiredAsync(false, (now - settings.FailedRetention()).ToUnixTimeSeconds(), cancellationToken);
        if (deleted > 0)
            _logger.LogInformation("Удалено устаревших заявок Ветис: {Count}", deleted);

        return deleted;
    }

    private async Task<int> DeleteExpiredAsync(bool completed, long createdBefore, CancellationToken cancellationToken)
    {
        var deleted = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var expired = await _applications.ListExpired(completed, createdBefore, BatchSize);
            if (expired.IsFailure || expired.Value.Count == 0)
                break;

            var removed = await _applications.Delete(expired.Value.Select(item => item.Id).ToList());
            if (removed.IsFailure)
            {
                _logger.LogWarning("Не удалось удалить устаревшие заявки Ветис: {Error}", removed.Error);
                break;
            }

            deleted += expired.Value.Count;
        }

        return deleted;
    }
}
