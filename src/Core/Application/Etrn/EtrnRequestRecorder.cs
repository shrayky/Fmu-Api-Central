using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Etrn;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class EtrnRequestRecorder
{
    private readonly IEtrnRequestRepository _requests;
    private readonly ILogger<EtrnRequestRecorder> _logger;
    private readonly TimeProvider _clock;

    public EtrnRequestRecorder(
        IEtrnRequestRepository requests,
        ILogger<EtrnRequestRecorder> logger,
        TimeProvider? clock = null)
    {
        _requests = requests;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Журнал вспомогательный: сбой записи не должен ронять обмен. Вход по сертификату и сессия сюда не попадают.
    /// </summary>
    public async Task RecordAsync(
        IEnumerable<EtrnExchange>? exchanges,
        string organizationId,
        EtrnOperator @operator)
    {
        foreach (var exchange in exchanges ?? [])
        {
            if (!EtrnMethods.Service.Contains(exchange.Method, StringComparer.Ordinal))
                continue;

            var entity = EtrnRequestEntity.From(exchange, organizationId, @operator, _clock.GetUtcNow());
            var saved = await _requests.Save(entity);
            if (saved.IsFailure)
                _logger.LogWarning("Команда ЭТрН {Method} не записана в журнал: {Error}", exchange.Method, saved.Error);
        }
    }
}

[AutoRegisterService(ServiceLifetime.Singleton)]
public sealed class EtrnAcceptanceGate
{
    private readonly object _sync = new();

    private readonly HashSet<string> _busy = new(StringComparer.Ordinal);

    public bool TryEnter(string documentId)
    {
        lock (_sync)
        {
            return _busy.Add(documentId);
        }
    }

    public void Exit(string documentId)
    {
        lock (_sync)
        {
            _busy.Remove(documentId);
        }
    }
}
