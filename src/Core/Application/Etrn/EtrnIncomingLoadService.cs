using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Etrn;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class EtrnIncomingLoadService : IEtrnIncomingLoader
{
    private const int ReconcileLimit = 50;

    private static readonly TimeSpan SendingLimit = TimeSpan.FromMinutes(10);

    private readonly IReadOnlyList<IEtrnOperator> _operators;
    private readonly IOrganizationRepository _organizations;
    private readonly IEtrnIncomingRepository _incoming;
    private readonly IEtrnSyncStateRepository _syncStates;
    private readonly EtrnRequestRecorder _recorder;
    private readonly ILogger<EtrnIncomingLoadService> _logger;
    private readonly TimeProvider _clock;

    public EtrnIncomingLoadService(
        IEnumerable<IEtrnOperator> operators,
        IOrganizationRepository organizations,
        IEtrnIncomingRepository incoming,
        IEtrnSyncStateRepository syncStates,
        EtrnRequestRecorder recorder,
        ILogger<EtrnIncomingLoadService> logger,
        TimeProvider? clock = null)
    {
        _operators = operators.ToList();
        _organizations = organizations;
        _incoming = incoming;
        _syncStates = syncStates;
        _recorder = recorder;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public string LastError { get; private set; } = "";

    public async Task<int> LoadAsync(CancellationToken cancellationToken)
    {
        LastError = "";
        var now = _clock.GetUtcNow();
        var organizations = await _organizations.All();
        await ReconcileAsync(organizations, now, cancellationToken);

        var saved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var organization in organizations)
        {
            foreach (var connection in organization.EtrnConnections.Where(item => item.Enable))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await LoadConnectionAsync(organization, connection, now, saved, cancellationToken);
            }
        }

        return saved.Count;
    }

    private async Task LoadConnectionAsync(
        OrganizationEntity organization,
        EtrnConnection connection,
        DateTimeOffset now,
        HashSet<string> saved,
        CancellationToken cancellationToken)
    {
        var sync = await ReadSyncAsync(organization, connection);
        var @operator = FindOperator(connection.Type);
        if (@operator == null)
        {
            await SaveErrorAsync(sync, $"Оператор {EtrnOperatorType.Label(connection.Type)} не поддерживается.");
            return;
        }

        if (string.IsNullOrWhiteSpace(organization.TrueApiIntegrationSettings?.DigitalSignature))
        {
            await SaveErrorAsync(sync, "Не выбран сертификат организации");
            return;
        }

        var context = Context(organization, connection, sync.Position);
        Result<EtrnFetchResult> fetched;
        try
        {
            fetched = await @operator.FetchAsync(context, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Опрос ЭТрН организации {Organization} прерван", organization.Name);
            await SaveErrorAsync(sync, ex.Message);
            return;
        }

        if (fetched.IsFailure)
        {
            await SaveErrorAsync(sync, fetched.Error);
            return;
        }

        var batch = fetched.Value;
        await _recorder.RecordAsync(batch.Exchanges, organization.Id, context.Operator);

        foreach (var document in batch.Documents)
        {
            var entity = document.ToEntity(context);
            if (string.IsNullOrWhiteSpace(entity.LastError) && !entity.HasQuantity)
                entity.LastError = EtrnDocumentStatus.NoQuantityError;

            var existing = await _incoming.Find(entity.Id);
            if (existing.IsFailure)
            {
                _logger.LogWarning("Документ ЭТрН {Id} не прочитан: {Error}", entity.Id, existing.Error);
                continue;
            }

            if (existing.Value is { } previous)
            {
                // Решённый документ и идущую отправку повторный опрос не перезаписывает.
                if (EtrnDocumentStatus.IsFinal(previous.Status) || previous.Status == EtrnDocumentStatus.Sending)
                    continue;

                // Снимок оператора не обновляется: переименование подключения уже записанный объект не меняет.
                entity.Operator = previous.Operator;
            }

            var written = await _incoming.Save(entity);
            if (written.IsFailure)
            {
                _logger.LogWarning("Документ ЭТрН {Id} не сохранён: {Error}", entity.Id, written.Error);
                continue;
            }

            saved.Add(entity.Id);
        }

        foreach (var operatorDocumentId in batch.ClosedDocumentIds.Distinct(StringComparer.Ordinal))
        {
            var closedId = EtrnIncomingDocumentEntity.BuildId(
                organization.Id,
                connection.Id,
                operatorDocumentId);
            var existing = await _incoming.Find(closedId);
            if (existing.IsFailure || existing.Value == null)
                continue;

            // Этап приёмки закрыт не нами: чужое решение уже не переписать.
            if (existing.Value.Status is not (EtrnDocumentStatus.Waiting or EtrnDocumentStatus.Sending))
                continue;

            EtrnDocumentState.Close(existing.Value);
            var written = await _incoming.Save(existing.Value);
            if (written.IsFailure)
                _logger.LogWarning("Закрытый документ ЭТрН {Id} не сохранён: {Error}", closedId, written.Error);
        }

        // Пара идёт по разобранному префиксу и при обрыве середины: следующий круг начнётся с непрочитанного события.
        sync.EventId = batch.Position.EventId;
        sync.DocumentId = batch.Position.DocumentId;
        if (!batch.Completed)
        {
            LastError = EtrnDocumentState.Truncate(batch.Error);
            sync.LastError = LastError;
            var failed = await _syncStates.Save(sync);
            if (failed.IsFailure)
                _logger.LogWarning("Состояние опроса ЭТрН {Connection} не сохранено: {Error}", connection.Id, failed.Error);
            return;
        }

        sync.LastError = "";
        sync.LastSuccessAt = now.ToString("O");
        var stored = await _syncStates.Save(sync);
        if (stored.IsFailure)
            _logger.LogWarning("Состояние опроса ЭТрН {Connection} не сохранено: {Error}", connection.Id, stored.Error);
    }

    /// <summary>
    /// Зависшая отправка сверяется с этапом: тот же этап — обратно в ожидание, другой — документ закрыт.
    /// </summary>
    private async Task ReconcileAsync(
        IReadOnlyList<OrganizationEntity> organizations,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var stale = await _incoming.StaleSending((now - SendingLimit).ToUnixTimeSeconds(), ReconcileLimit);
        if (stale.IsFailure)
        {
            _logger.LogWarning("Зависшие отправки ЭТрН не прочитаны: {Error}", stale.Error);
            return;
        }

        foreach (var document in stale.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var organization = organizations.FirstOrDefault(item =>
                string.Equals(item.Id, document.OrganizationId, StringComparison.OrdinalIgnoreCase));
            var connection = organization?.EtrnConnections.FirstOrDefault(item =>
                string.Equals(item.Id, document.Operator.ConnectionId, StringComparison.OrdinalIgnoreCase));
            if (organization == null || connection == null)
                continue;

            var @operator = FindOperator(connection.Type);
            if (@operator == null)
                continue;
            if (string.IsNullOrWhiteSpace(organization.TrueApiIntegrationSettings?.DigitalSignature))
                continue;

            var context = Context(organization, connection, EtrnPollPosition.Empty);
            Result<EtrnStageResult> stage;
            try
            {
                stage = await @operator.ReadStageAsync(context, document.OperatorDocumentId, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Этап документа ЭТрН {Id} не прочитан", document.Id);
                continue;
            }

            if (stage.IsFailure)
                continue;

            await _recorder.RecordAsync(stage.Value.Exchanges, organization.Id, context.Operator);
            if (!stage.Value.Completed)
                continue;

            EtrnDocumentState.Reconcile(document, stage.Value.StillAcceptance, now);
            var saved = await _incoming.Save(document);
            if (saved.IsFailure)
                _logger.LogWarning("Зависшая отправка ЭТрН {Id} не сохранена: {Error}", document.Id, saved.Error);
        }
    }

    private async Task<EtrnSyncState> ReadSyncAsync(OrganizationEntity organization, EtrnConnection connection)
    {
        var found = await _syncStates.Find(connection.Id);
        if (found.IsSuccess && found.Value != null)
            return found.Value;

        return new EtrnSyncState
        {
            Id = EtrnSyncState.BuildId(connection.Id),
            OrganizationId = organization.Id,
            ConnectionId = connection.Id
        };
    }

    private async Task SaveErrorAsync(EtrnSyncState sync, string? error)
    {
        LastError = EtrnDocumentState.Truncate(error);
        sync.LastError = LastError;
        var saved = await _syncStates.Save(sync);
        if (saved.IsFailure)
            _logger.LogWarning("Ошибка опроса ЭТрН {Connection} не записана: {Error}", sync.ConnectionId, saved.Error);
    }

    private IEtrnOperator? FindOperator(string type) =>
        _operators.FirstOrDefault(item => string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase));

    private static EtrnOperatorContext Context(
        OrganizationEntity organization,
        EtrnConnection connection,
        EtrnPollPosition position) => new()
    {
        OrganizationId = organization.Id,
        Inn = organization.Inn,
        Connection = connection,
        CertificateNumber = organization.TrueApiIntegrationSettings?.DigitalSignature ?? "",
        CertificatePassword = organization.TrueApiIntegrationSettings?.Password ?? "",
        Position = position
    };
}
