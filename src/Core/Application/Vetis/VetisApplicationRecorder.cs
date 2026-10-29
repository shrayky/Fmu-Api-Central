using Domain.Attributes;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisApplicationRecorder
{
    private readonly IVetisApplicationRepository _applications;
    private readonly ILogger<VetisApplicationRecorder> _logger;
    private readonly TimeProvider _clock;

    public VetisApplicationRecorder(
        IVetisApplicationRepository applications,
        ILogger<VetisApplicationRecorder> logger,
        TimeProvider? clock = null)
    {
        _applications = applications;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Журнал заявок вспомогательный: сбой записи не должен ломать сам обмен с Ветис.
    /// </summary>
    public async Task RecordAsync(VetisExchange exchange, string organizationId, string enterpriseGuid)
    {
        var application = VetisApplicationEntity.From(exchange, organizationId, enterpriseGuid, _clock.GetUtcNow());
        var saved = await _applications.Save(application);
        if (saved.IsFailure)
            _logger.LogWarning("Заявка Ветис {Operation} не записана в журнал: {Error}", exchange.Operation, saved.Error);
    }
}
