using Application.Dadata;
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
    private readonly DadataSalePointResolver _dadataSalePoints;
    private readonly ILogger<EtrnIncomingLoadService> _logger;
    private readonly TimeProvider _clock;

    public EtrnIncomingLoadService(
        IEnumerable<IEtrnOperator> operators,
        IOrganizationRepository organizations,
        IEtrnIncomingRepository incoming,
        IEtrnSyncStateRepository syncStates,
        EtrnRequestRecorder recorder,
        DadataSalePointResolver dadataSalePoints,
        ILogger<EtrnIncomingLoadService> logger,
        TimeProvider? clock = null)
    {
        _operators = operators.ToList();
        _organizations = organizations;
        _incoming = incoming;
        _syncStates = syncStates;
        _recorder = recorder;
        _dadataSalePoints = dadataSalePoints;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public string LastError { get; private set; } = "";

    public async Task SyncOpenAsync(string id, CancellationToken cancellationToken)
    {
        var found = await _incoming.Find(id);
        if (found.IsFailure || found.Value == null)
            return;

        var document = found.Value;
        var cache = new DadataLookupCache();
        if (document.Status != EtrnDocumentStatus.Waiting)
            return;
        if (!string.Equals(document.Operator.Type, EtrnOperatorType.Saby, StringComparison.OrdinalIgnoreCase))
            return;

        var organization = await _organizations.GetById(document.OrganizationId);
        if (organization.IsFailure)
            return;

        var connection = (organization.Value.EtrnConnections ?? []).FirstOrDefault(item =>
            string.Equals(item.Id, document.Operator.ConnectionId, StringComparison.OrdinalIgnoreCase));
        if (connection == null
            || !string.Equals(connection.Type, EtrnOperatorType.Saby, StringComparison.OrdinalIgnoreCase))
            return;
        if (string.IsNullOrWhiteSpace(organization.Value.TrueApiIntegrationSettings?.DigitalSignature))
            return;

        var @operator = FindOperator(connection.Type);
        if (@operator == null)
            return;

        var context = Context(organization.Value, connection, EtrnPollPosition.Empty);
        Result<EtrnStageResult> stage;
        try
        {
            stage = await @operator.ReadStageAsync(context, document.OperatorDocumentId, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Состояние ЭТрН {Id} при открытии не прочитано", document.Id);
            return;
        }

        if (stage.IsFailure || !stage.Value.Completed)
            return;

        await _recorder.RecordAsync(stage.Value.Exchanges, document.OrganizationId, context.Operator);
        if (stage.Value.Status is EtrnDocumentStatus.Accepted or EtrnDocumentStatus.Rejected)
        {
            document.Status = stage.Value.Status;
            document.Action = stage.Value.Action.Length > 0
                ? stage.Value.Action
                : stage.Value.Status == EtrnDocumentStatus.Rejected
                    ? EtrnAcceptanceAction.Rejected
                    : EtrnAcceptanceAction.Received;
            document.LastError = "";
            document.SendingAt = null;
            document.SendingAtUnix = 0;
            await SaveWaitingAsync(organization.Value, document, cache, cancellationToken);
            return;
        }

        if (stage.Value.CloseDocument)
        {
            var name = stage.Value.StateName.Length > 0 ? stage.Value.StateName : stage.Value.Error;
            EtrnDocumentState.Finish(document, name);
            await SaveWaitingAsync(organization.Value, document, cache, cancellationToken);
            return;
        }

        // Список показывает имя этапа из заметки. Открытая приёмка эту заметку снимает.
        if (stage.Value.StillAcceptance)
        {
            if (!EtrnDocumentState.IsStageNote(document.LastError))
                return;

            document.LastError = "";
            await SaveWaitingAsync(organization.Value, document, cache, cancellationToken);
            return;
        }

        if (stage.Value.Stage.Length == 0)
            return;
        if (document.LastError.Length > 0 && !EtrnDocumentState.IsStageNote(document.LastError))
            return;

        var note = EtrnDocumentState.StageUnavailable(stage.Value.Stage);
        if (document.LastError == note)
            return;

        document.LastError = note;
        await SaveWaitingAsync(organization.Value, document, cache, cancellationToken);
    }

    public async Task<int> LoadAsync(CancellationToken cancellationToken)
    {
        LastError = "";
        var now = _clock.GetUtcNow();
        var organizations = await _organizations.All();
        await ReconcileAsync(organizations, now, cancellationToken);

        var saved = new HashSet<string>(StringComparer.Ordinal);
        var cache = new DadataLookupCache();
        foreach (var organization in organizations)
        {
            foreach (var connection in (organization.EtrnConnections ?? []).Where(item => item.Enable))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await LoadConnectionAsync(organization, connection, now, saved, cache, cancellationToken);
            }
        }

        return saved.Count;
    }

    private async Task LoadConnectionAsync(
        OrganizationEntity organization,
        EtrnConnection connection,
        DateTimeOffset now,
        HashSet<string> saved,
        DadataLookupCache cache,
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

        foreach (var document in batch.Documents ?? [])
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
                // Решённый документ, чужой ящик и идущую отправку повторный опрос не перезаписывает.
                if (EtrnDocumentStatus.IsFinal(previous.Status)
                    || previous.Status == EtrnDocumentStatus.Sending
                    || previous.Status == EtrnDocumentStatus.Foreign)
                {
                    // Тело накладной не трогаем, но пустую точку пишем: иначе она не попадёт в отбор.
                    await KeepSalePointAsync(organization, previous, cache, cancellationToken);
                    continue;
                }

                // Снимок оператора не обновляется: переименование подключения уже записанный объект не меняет.
                entity.Operator = previous.Operator;
                // Ручной выбор и подобранная точка переживают повторный опрос.
                entity.SalePointUid = previous.SalePointUid;
            }

            if (entity.SalePointUid.Length == 0)
                entity.SalePointUid = await MatchOrAskDadata(
                    organization,
                    entity.DeliveryAddress,
                    cache,
                    cancellationToken);

            var written = await _incoming.Save(entity);
            if (written.IsFailure)
            {
                _logger.LogWarning("Документ ЭТрН {Id} не сохранён: {Error}", entity.Id, written.Error);
                continue;
            }

            saved.Add(entity.Id);
        }

        await ApplyListedStatesAsync(organization, connection, batch.ListedStates, cache, cancellationToken);

        foreach (var operatorDocumentId in (batch.ClosedDocumentIds ?? []).Distinct(StringComparer.Ordinal))
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
            await AssignSalePointIfEmpty(organization, existing.Value, cache, cancellationToken);
            var written = await _incoming.Save(existing.Value);
            if (written.IsFailure)
                _logger.LogWarning("Закрытый документ ЭТрН {Id} не сохранён: {Error}", closedId, written.Error);
        }

        // Пара идёт по разобранному префиксу и при обрыве середины: следующий круг начнётся с непрочитанного события.
        sync.EventId = batch.Position.EventId;
        sync.DocumentId = batch.Position.DocumentId;
        sync.EventAt = batch.Position.EventAt;
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
    /// Список уже принёс состояние. Карточки без титула здесь нет, поэтому правим только сохранённую накладную.
    /// </summary>
    private async Task ApplyListedStatesAsync(
        OrganizationEntity organization,
        EtrnConnection connection,
        IReadOnlyList<EtrnListedState>? states,
        DadataLookupCache cache,
        CancellationToken cancellationToken)
    {
        foreach (var update in states ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (update.OperatorDocumentId.Length == 0)
                continue;

            var id = EtrnIncomingDocumentEntity.BuildId(organization.Id, connection.Id, update.OperatorDocumentId);
            var existing = await _incoming.Find(id);
            if (existing.IsFailure || existing.Value == null)
                continue;

            var document = existing.Value;
            if (document.Status is not (EtrnDocumentStatus.Waiting or EtrnDocumentStatus.Sending))
                continue;

            if (update.Status is EtrnDocumentStatus.Accepted or EtrnDocumentStatus.Rejected)
            {
                document.Status = update.Status;
                document.Action = update.Action.Length > 0
                    ? update.Action
                    : update.Status == EtrnDocumentStatus.Rejected
                        ? EtrnAcceptanceAction.Rejected
                        : EtrnAcceptanceAction.Received;
                document.LastError = "";
                document.SendingAt = null;
                document.SendingAtUnix = 0;
                await SaveWaitingAsync(organization, document, cache, cancellationToken);
                continue;
            }

            if (update.Status == EtrnDocumentStatus.Closed)
            {
                EtrnDocumentState.Finish(document, update.Error);
                await SaveWaitingAsync(organization, document, cache, cancellationToken);
                continue;
            }

            if (update.Status != EtrnDocumentStatus.Waiting || update.Error.Length == 0)
                continue;

            if (document.Status != EtrnDocumentStatus.Waiting)
                continue;

            if (document.LastError.Length > 0 && !EtrnDocumentState.IsStageNote(document.LastError))
                continue;

            if (document.LastError == update.Error)
                continue;

            document.LastError = update.Error;
            await SaveWaitingAsync(organization, document, cache, cancellationToken);
        }
    }

    /// <summary>
    /// Пустое поле заполняется и у накладной, которую опрос больше не переписывает.
    /// Уже выбранный uid не меняется. Совпадение ищется по адресу, который уже лежит в документе.
    /// </summary>
    private async Task KeepSalePointAsync(
        OrganizationEntity organization,
        EtrnIncomingDocumentEntity document,
        DadataLookupCache cache,
        CancellationToken cancellationToken)
    {
        if (document.SalePointUid.Length > 0)
            return;

        await AssignSalePointIfEmpty(organization, document, cache, cancellationToken);
        var written = await _incoming.Save(document);
        if (written.IsFailure)
            _logger.LogWarning("Точка ЭТрН {Id} не сохранена: {Error}", document.Id, written.Error);
    }

    private async Task AssignSalePointIfEmpty(
        OrganizationEntity organization,
        EtrnIncomingDocumentEntity document,
        DadataLookupCache cache,
        CancellationToken cancellationToken)
    {
        if (document.SalePointUid.Length > 0)
            return;

        document.SalePointUid = await MatchOrAskDadata(
            organization,
            document.DeliveryAddress,
            cache,
            cancellationToken);
    }

    /// <summary>
    /// Совпадение по адресу или синониму, затем ФИАС дома из стандартизации DaData.
    /// </summary>
    private async Task<string> MatchOrAskDadata(
        OrganizationEntity organization,
        string address,
        DadataLookupCache cache,
        CancellationToken cancellationToken)
    {
        var uid = SalePointMatch.Find(organization.SalePoints, address);
        if (uid.Length > 0)
            return uid;

        return await _dadataSalePoints.Resolve(organization, address, cache, cancellationToken);
    }

    private async Task SaveWaitingAsync(
        OrganizationEntity organization,
        EtrnIncomingDocumentEntity document,
        DadataLookupCache cache,
        CancellationToken cancellationToken)
    {
        await AssignSalePointIfEmpty(organization, document, cache, cancellationToken);
        var written = await _incoming.Save(document);
        if (written.IsFailure)
            _logger.LogWarning("Состояние ЭТрН {Id} не сохранено: {Error}", document.Id, written.Error);
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
            var connection = (organization?.EtrnConnections ?? []).FirstOrDefault(item =>
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
