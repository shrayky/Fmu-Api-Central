using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Etrn;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class EtrnAcceptanceService
{
    private readonly IReadOnlyList<IEtrnOperator> _operators;
    private readonly IOrganizationRepository _organizations;
    private readonly IEtrnIncomingRepository _incoming;
    private readonly EtrnRequestRecorder _recorder;
    private readonly EtrnIncomingQueryService _query;
    private readonly EtrnAcceptanceGate _gate;
    private readonly ILogger<EtrnAcceptanceService> _logger;
    private readonly TimeProvider _clock;

    public EtrnAcceptanceService(
        IEnumerable<IEtrnOperator> operators,
        IOrganizationRepository organizations,
        IEtrnIncomingRepository incoming,
        EtrnRequestRecorder recorder,
        EtrnIncomingQueryService query,
        EtrnAcceptanceGate gate,
        ILogger<EtrnAcceptanceService> logger,
        TimeProvider? clock = null)
    {
        _operators = operators.ToList();
        _organizations = organizations;
        _incoming = incoming;
        _recorder = recorder;
        _query = query;
        _gate = gate;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Result<EtrnIncomingView>> AcceptAsync(
        string id,
        EtrnAcceptCommand? command,
        CancellationToken cancellationToken)
    {
        command ??= new EtrnAcceptCommand();
        if (!_gate.TryEnter(id))
            return Result.Failure<EtrnIncomingView>(EtrnDocumentState.AlreadyRunning);

        try
        {
            return await SendAsync(id, command, cancellationToken);
        }
        finally
        {
            _gate.Exit(id);
        }
    }

    private async Task<Result<EtrnIncomingView>> SendAsync(
        string id,
        EtrnAcceptCommand command,
        CancellationToken cancellationToken)
    {
        var found = await _incoming.Find(id);
        if (found.IsFailure)
            return Result.Failure<EtrnIncomingView>(found.Error);
        if (found.Value == null)
            return Result.Failure<EtrnIncomingView>("Документ не найден.");

        var document = found.Value;
        if (document.Status == EtrnDocumentStatus.Sending)
            return Result.Failure<EtrnIncomingView>(EtrnDocumentState.AlreadyRunning);
        if (EtrnDocumentStatus.IsFinal(document.Status))
            return Result.Failure<EtrnIncomingView>(EtrnDocumentState.AlreadyClosed);

        var organization = await _organizations.GetById(document.OrganizationId);
        if (organization.IsFailure)
            return Result.Failure<EtrnIncomingView>(organization.Error);

        var connection = organization.Value.EtrnConnections.FirstOrDefault(item =>
            string.Equals(item.Id, document.Operator.ConnectionId, StringComparison.OrdinalIgnoreCase));
        if (connection == null)
            return Result.Failure<EtrnIncomingView>("Подключение оператора не найдено.");

        if (string.IsNullOrWhiteSpace(organization.Value.TrueApiIntegrationSettings?.DigitalSignature))
            return Result.Failure<EtrnIncomingView>("Не выбран сертификат организации");

        var @operator = _operators.FirstOrDefault(item =>
            string.Equals(item.Type, connection.Type, StringComparison.OrdinalIgnoreCase));
        if (@operator == null)
            return Result.Failure<EtrnIncomingView>($"Оператор {EtrnOperatorType.Label(connection.Type)} не поддерживается.");

        var decision = EtrnAcceptance.Decide(
            document.Lines,
            command.Lines ?? [],
            command.Comment,
            command.Intent);
        if (decision.IsFailure)
            return Result.Failure<EtrnIncomingView>(decision.Error);

        var now = _clock.GetUtcNow();
        var started = EtrnDocumentState.StartSending(document, now);
        if (started.IsFailure)
            return Result.Failure<EtrnIncomingView>(started.Error);

        var pending = await _incoming.Save(document);
        if (pending.IsFailure)
            return Result.Failure<EtrnIncomingView>("Приёмка не сохранена: " + pending.Error);

        var context = Context(organization.Value, connection);
        Result<EtrnExecuteResult> executed;
        try
        {
            executed = await @operator.ExecuteAsync(
                context,
                new EtrnExecuteCommand { Document = document, Decision = decision.Value },
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Приёмка ЭТрН {Id} прервана", document.Id);
            return await FailAsync(document, ex.Message);
        }

        if (executed.IsFailure)
            return await FailAsync(document, executed.Error);

        await _recorder.RecordAsync(executed.Value.Exchanges, document.OrganizationId, context.Operator);
        if (!executed.Value.Completed)
            return await FailAsync(
                document,
                string.IsNullOrWhiteSpace(executed.Value.Error) ? "Saby не подтвердила отправку титула." : executed.Value.Error);

        var succeeded = EtrnDocumentState.Succeed(document, decision.Value, now);
        if (succeeded.IsFailure)
            return Result.Failure<EtrnIncomingView>(succeeded.Error);

        var stored = await _incoming.Save(document);
        if (stored.IsFailure)
            return Result.Failure<EtrnIncomingView>("Титул отправлен, но документ не сохранён: " + stored.Error);

        return await _query.GetAsync(document.Id);
    }

    /// <summary>
    /// Сбой на любом шаге отправки возвращает документ в ожидание: повтор должен быть возможен.
    /// </summary>
    private async Task<Result<EtrnIncomingView>> FailAsync(EtrnIncomingDocumentEntity document, string? error)
    {
        EtrnDocumentState.Fail(document, error);
        var saved = await _incoming.Save(document);
        if (saved.IsFailure)
            return Result.Failure<EtrnIncomingView>("Отправка не удалась, документ не сохранён: " + saved.Error);

        return Result.Failure<EtrnIncomingView>(EtrnDocumentState.Truncate(error));
    }

    private static EtrnOperatorContext Context(
        OrganizationEntity organization,
        EtrnConnection connection) => new()
    {
        OrganizationId = organization.Id,
        Inn = organization.Inn,
        Connection = connection,
        CertificateNumber = organization.TrueApiIntegrationSettings?.DigitalSignature ?? "",
        CertificatePassword = organization.TrueApiIntegrationSettings?.Password ?? ""
    };
}
