using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Entitys.Organization.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisIncomingQuenchService
{
    private readonly IVetisGateway _gateway;
    private readonly IVetisIncomingRepository _incoming;
    private readonly IOrganizationRepository _organizations;
    private readonly VetisApplicationRecorder _recorder;
    private readonly VetisIncomingQueryService _query;
    private readonly IParametersService _parameters;
    private readonly VetisQuenchProgressStore _progress;
    private readonly TimeProvider _clock;

    public VetisIncomingQuenchService(
        IVetisGateway gateway,
        IVetisIncomingRepository incoming,
        IOrganizationRepository organizations,
        VetisApplicationRecorder recorder,
        VetisIncomingQueryService query,
        IParametersService parameters,
        VetisQuenchProgressStore progress,
        TimeProvider? clock = null)
    {
        _gateway = gateway;
        _incoming = incoming;
        _organizations = organizations;
        _recorder = recorder;
        _query = query;
        _parameters = parameters;
        _progress = progress;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Result<VetisQuenchReport>> QuenchAsync(
        string documentId,
        IReadOnlyList<VetisQuenchFact> facts,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? onlyUuids = null,
        string? author = null)
    {
        if (!await _progress.TryEnter(documentId, cancellationToken))
            return Result.Failure<VetisQuenchReport>(VetisQuench.AlreadyRunning);

        try
        {
            var found = await _incoming.Find(documentId);
            if (found.IsFailure)
                return Result.Failure<VetisQuenchReport>(found.Error);
            if (found.Value == null)
                return Result.Failure<VetisQuenchReport>("Документ не найден.");

            var document = found.Value;
            var only = onlyUuids == null
                ? null
                : new HashSet<string>(onlyUuids.Where(item => !string.IsNullOrWhiteSpace(item)), StringComparer.OrdinalIgnoreCase);
            if (!document.Vsds.Any(vsd => Chosen(vsd, only)))
                return Result.Failure<VetisQuenchReport>("Нет ВСД для гашения.");

            var organization = await _organizations.GetById(document.OrganizationId);
            if (organization.IsFailure)
                return Result.Failure<VetisQuenchReport>(organization.Error);

            var server = (await _parameters.Current()).VetisSettings ?? new();
            var call = VetisCallFactory.ForOrganization(server, organization.Value.VetisSettings);
            if (call == null)
                return Result.Failure<VetisQuenchReport>("Настройки Ветис не заполнены, гашение не отправлено.");

            var requireVehicle = server.RequireVehicleForQuench;

            var requested = (facts ?? [])
                .GroupBy(item => (item.Uuid ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Key.Length > 0)
                .ToDictionary(group => group.Key, group => group.Last().ActualVolume ?? "", StringComparer.OrdinalIgnoreCase);

            var report = new VetisQuenchReport();
            var failed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void Fail(string uuid, string message)
            {
                report.Errors.Add(new VetisQuenchError { Uuid = uuid, Message = message });
                failed[uuid] = message;
            }

            var quenchedAt = VetisDateTime.Format(_clock.GetLocalNow());
            var total = document.Vsds.Count(vsd => Chosen(vsd, only));
            var index = 0;
            // Merge подменяет элемент того же списка, и прямой перебор обрывается.
            foreach (var vsd in document.Vsds.ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!VetisQuench.CanQuench(vsd.Status) || (only != null && !only.Contains(vsd.Uuid)))
                {
                    report.Skipped++;
                    continue;
                }

                if (requireVehicle && !VetisTransport.HasVehicleNumber(vsd.RawXml))
                {
                    Fail(vsd.Uuid, "В ВСД нет номера автомобиля, гашение не отправлено.");
                    continue;
                }

                string? actual = requested.TryGetValue(vsd.Uuid, out var fact) ? fact : null;
                var order = VetisQuench.Decide(vsd.Volume, actual);
                if (order.IsFailure)
                {
                    Fail(vsd.Uuid, order.Error);
                    continue;
                }

                index++;
                _progress.Set(document.Id, new VetisQuenchProgress
                {
                    Running = true,
                    Index = index,
                    Total = total,
                    Uuid = vsd.Uuid,
                    IssueNumber = vsd.IssueNumber,
                    Stage = "sending"
                });

                var result = await _gateway.QuenchAsync(call, vsd.RawXml, order.Value, vsd.Uuid, cancellationToken);
                await _recorder.RecordAsync(result.Exchange, document.OrganizationId, document.EnterpriseGuid);
                if (!result.Exchange.Completed && !AlreadyQuenched(result.Exchange.Error))
                {
                    Fail(
                        vsd.Uuid,
                        string.IsNullOrWhiteSpace(result.Exchange.Error) ? "Ветис не погасил ВСД." : result.Exchange.Error);
                    continue;
                }

                Action<VetisIncomingVsd> apply = target =>
                {
                    target.Status = "UTILIZED";
                    target.LastError = "";
                };
                if (result.Exchange.Completed)
                {
                    apply = target =>
                    {
                        target.Status = string.IsNullOrWhiteSpace(result.Status) ? "UTILIZED" : result.Status;
                        if (!string.IsNullOrWhiteSpace(result.ReceivedAt))
                            target.ReceivedAt = result.ReceivedAt;

                        target.ActualVolume = order.Value.ActualVolume;
                        target.LastError = "";
                    };
                }

                // Сохраняем сразу: обрыв на следующем ВСД не должен откатить уже принятое Меркурием гашение.
                var stored = await StoreQuench(document.Id, vsd.Uuid, apply, author, quenchedAt);
                if (stored.Outcome == QuenchStore.Failed)
                    return Result.Failure<VetisQuenchReport>("ВСД погашены, но документ не сохранён: " + stored.Error);
                if (stored.Outcome == QuenchStore.Missing)
                {
                    Fail(vsd.Uuid, "ВСД погашен в Меркурии, но в документе его уже нет.");
                    continue;
                }

                report.Quenched++;
            }

            await RememberErrors(document.Id, failed);

            var view = await _query.GetAsync(document.Id);
            report.Document = view.IsSuccess ? view.Value : null;
            return Result.Success(report);
        }
        finally
        {
            // Обрыв на очередном ВСД не должен оставлять «2 из 5» до следующего гашения.
            _progress.Clear(documentId);
            _progress.Exit(documentId);
        }
    }

    private static bool Chosen(VetisIncomingVsd vsd, HashSet<string>? only) =>
        VetisQuench.CanQuench(vsd.Status) && (only == null || only.Contains(vsd.Uuid));

    private const string AlreadyUtilized =
        "должен быть в состоянии \"оформлен\" (т.е. не погашен)";

    /// <summary>
    /// Повторное гашение Меркурий отклоняет этой фразой, когда ВСД уже погашен, а локальная копия ещё «оформлен».
    /// </summary>
    private static bool AlreadyQuenched(string? error) =>
        NormalizeMercury(error).Contains(AlreadyUtilized, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeMercury(string? error)
    {
        var text = (error ?? "")
            .Replace('«', '"')
            .Replace('»', '"')
            .Replace('“', '"')
            .Replace('”', '"');
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private enum QuenchStore
    {
        Saved,
        Missing,
        Failed
    }

    private readonly record struct QuenchStored(QuenchStore Outcome, string Error);

    /// <summary>
    /// Документ читается заново: за время гашения загрузка могла дописать ВСД, и сохранение снимка их сотрёт.
    /// </summary>
    private async Task<QuenchStored> StoreQuench(
        string documentId,
        string uuid,
        Action<VetisIncomingVsd> apply,
        string? author,
        string quenchedAt)
    {
        var fresh = await _incoming.Find(documentId);
        if (fresh.IsFailure)
            return new QuenchStored(QuenchStore.Failed, fresh.Error);
        if (fresh.Value == null)
            return new QuenchStored(QuenchStore.Missing, "");

        var target = fresh.Value.Vsds.FirstOrDefault(item =>
            string.Equals(item.Uuid, uuid, StringComparison.OrdinalIgnoreCase));
        if (target == null)
            return new QuenchStored(QuenchStore.Missing, "");

        apply(target);
        fresh.Value.Merge([target]);
        if (!string.IsNullOrWhiteSpace(author))
        {
            fresh.Value.QuenchedBy = author.Trim();
            fresh.Value.QuenchedAt = quenchedAt;
        }

        var saved = await _incoming.Save(fresh.Value);
        return saved.IsFailure
            ? new QuenchStored(QuenchStore.Failed, saved.Error)
            : new QuenchStored(QuenchStore.Saved, "");
    }

    /// <summary>
    /// Причины отказа пишутся в документ одной записью. Сбой записи отчёт не отменяет: тексты уже в нём.
    /// </summary>
    private async Task RememberErrors(string documentId, IReadOnlyDictionary<string, string> failed)
    {
        if (failed.Count == 0)
            return;

        var fresh = await _incoming.Find(documentId);
        if (fresh.IsFailure || fresh.Value == null)
            return;

        var changed = false;
        foreach (var vsd in fresh.Value.Vsds)
        {
            if (!failed.TryGetValue(vsd.Uuid, out var message) || string.Equals(vsd.LastError, message, StringComparison.Ordinal))
                continue;

            vsd.LastError = message;
            changed = true;
        }

        if (changed)
            await _incoming.Save(fresh.Value);
    }
}
