using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisIncomingLoadService : IVetisIncomingLoader
{
    // Предохранитель от бесконечного перелистывания, если Ветис всегда отдаёт полную страницу.
    private const int MaxPagesPerSlice = 500;

    private readonly IVetisGateway _gateway;
    private readonly IOrganizationRepository _organizations;
    private readonly IVetisIncomingRepository _incoming;
    private readonly VetisApplicationRecorder _recorder;
    private readonly IParametersService _parameters;
    private readonly ILogger<VetisIncomingLoadService> _logger;
    private readonly TimeProvider _clock;

    public VetisIncomingLoadService(
        IVetisGateway gateway,
        IOrganizationRepository organizations,
        IVetisIncomingRepository incoming,
        VetisApplicationRecorder recorder,
        IParametersService parameters,
        ILogger<VetisIncomingLoadService> logger,
        TimeProvider? clock = null)
    {
        _gateway = gateway;
        _organizations = organizations;
        _incoming = incoming;
        _recorder = recorder;
        _parameters = parameters;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var server = (await _parameters.Current()).VetisSettings ?? new();
        if (!server.Enable)
            return;

        foreach (var organization in await _organizations.All())
        {
            if (organization.VetisSettings.Enable)
                await LoadOrganizationAsync(organization, server, cancellationToken);
        }
    }

    private async Task LoadOrganizationAsync(OrganizationEntity organization, VetisSettings server, CancellationToken cancellationToken)
    {
        var call = VetisCallFactory.ForOrganization(server, organization.VetisSettings);
        if (call == null)
        {
            _logger.LogDebug("Организация {Organization}: настройки Ветис не заполнены, загрузка ВСД пропущена", organization.Name);
            return;
        }

        foreach (var enterprise in organization.VetisSettings.Enterprises.Where(item => item.Participates))
        {
            var cursor = await ReadEnterpriseAsync(call, organization, enterprise, server, cancellationToken);
            if (cursor.IsFailure)
            {
                _logger.LogWarning(
                    "Загрузка ВСД организации {Organization}, площадка {Enterprise}: {Error}",
                    organization.Name,
                    enterprise.Presentation.Length > 0 ? enterprise.Presentation : enterprise.Guid,
                    cursor.Error);
                continue;
            }

            await ShiftCursorAsync(organization.Id, enterprise.Guid, cursor.Value);
        }
    }

    /// <summary>
    /// Окно, которое Ветис отклонил из-за длины, режется пополам и читается заново. Курсор — результат только полностью прочитанной площадки.
    /// </summary>
    private async Task<Result<DateTimeOffset>> ReadEnterpriseAsync(
        VetisCall call,
        OrganizationEntity organization,
        OrganizationEnterprise enterprise,
        VetisSettings server,
        CancellationToken cancellationToken)
    {
        var end = _clock.GetLocalNow();
        var begin = IncomingChangesWindow.Begin(end, organization.VetisSettings.IncomingFrom, enterprise.IncomingFrom);
        var pageSize = server.IncomingPageSize();
        var pending = new Queue<(DateTimeOffset Begin, DateTimeOffset End)>(IncomingChangesWindow.Slices(begin, end));
        DateTimeOffset? latestUpdate = null;
        DateTimeOffset? coveredEnd = null;

        while (pending.Count > 0)
        {
            var slice = pending.Dequeue();
            var sliceRead = false;
            for (var page = 0; page < MaxPagesPerSlice; page++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _gateway.GetIncomingAsync(
                    call,
                    new VetisIncomingRequest
                    {
                        EnterpriseGuid = enterprise.Guid,
                        Begin = slice.Begin,
                        End = slice.End,
                        Offset = page * pageSize,
                        Count = pageSize
                    },
                    cancellationToken);
                await _recorder.RecordAsync(result.Exchange, organization.Id, enterprise.Guid);

                if (!result.Exchange.Completed)
                {
                    if (!IncomingChangesWindow.RejectsInterval(result.Exchange.Error, slice.Begin, slice.End))
                        return Result.Failure<DateTimeOffset>(result.Exchange.Error ?? "Ветис не завершил заявку.");

                    var middle = IncomingChangesWindow.Midpoint(slice.Begin, slice.End);
                    pending.Enqueue((slice.Begin, middle));
                    pending.Enqueue((middle, slice.End));
                    sliceRead = true;
                    break;
                }

                var saved = await SaveDocumentsAsync(organization.Id, enterprise.Guid, result.Documents);
                if (saved.IsFailure)
                    return Result.Failure<DateTimeOffset>("ВСД получены, но не сохранены: " + saved.Error);

                latestUpdate = Latest(latestUpdate, result.Documents);
                if (result.PageCount < pageSize)
                {
                    coveredEnd = slice.End;
                    sliceRead = true;
                    break;
                }
            }

            if (!sliceRead)
                return Result.Failure<DateTimeOffset>($"Срез ВСД не прочитан: страниц больше {MaxPagesPerSlice}.");
        }

        var cursor = IncomingChangesWindow.NextFrom(latestUpdate, coveredEnd);
        return cursor == null
            ? Result.Failure<DateTimeOffset>("Ветис не вернул ни одной страницы.")
            : Result.Success(cursor.Value);
    }

    private async Task<Result> SaveDocumentsAsync(string organizationId, string enterpriseGuid, IReadOnlyList<VetisIncomingVsd> vsds)
    {
        var grouped = VetisIncomingDocumentEntity.Group(organizationId, enterpriseGuid, vsds);
        var owner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in grouped)
        {
            foreach (var vsd in document.Vsds)
                owner[vsd.Uuid] = document.Id;
        }

        var keep = new List<string>();
        var uuids = new List<string>();
        foreach (var document in grouped)
        {
            var existing = await _incoming.Find(document.Id);
            if (existing.IsFailure)
                return existing;

            var target = existing.Value ?? document;
            if (existing.Value != null)
            {
                target.Merge(document.Vsds);
                // Обе накладные страницы попадут в keep, и Release старый документ не тронет.
                var movedAway = target.Vsds
                    .Where(item => owner.TryGetValue(item.Uuid, out var home)
                        && !string.Equals(home, target.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Uuid)
                    .ToArray();
                target.Drop(movedAway);
            }

            var saved = await _incoming.Save(target);
            if (saved.IsFailure)
                return saved;

            keep.Add(target.Id);
            uuids.AddRange(target.Vsds.Select(item => item.Uuid));
        }

        if (uuids.Count == 0)
            return Result.Success();

        return await _incoming.Release(organizationId, enterpriseGuid, uuids, keep);
    }

    /// <summary>
    /// Карточка читается заново: за время загрузки её могли отредактировать, и записывать нужно только дату площадки.
    /// </summary>
    private async Task ShiftCursorAsync(string organizationId, string enterpriseGuid, DateTimeOffset cursor)
    {
        var fresh = await _organizations.GetById(organizationId);
        var target = fresh.IsSuccess
            ? fresh.Value.VetisSettings.Enterprises.FirstOrDefault(item => string.Equals(item.Guid, enterpriseGuid, StringComparison.OrdinalIgnoreCase))
            : null;
        if (target == null)
            return;

        target.IncomingFrom = VetisDateTime.Format(cursor);
        var updated = await _organizations.Update(fresh.Value);
        if (updated.IsFailure)
            _logger.LogWarning("Дата получения площадки {Enterprise} не записана: {Error}", enterpriseGuid, updated.Error);
    }

    private static DateTimeOffset? Latest(DateTimeOffset? current, IEnumerable<VetisIncomingVsd> vsds)
    {
        foreach (var vsd in vsds)
        {
            if (VetisDateTime.TryParse(vsd.UpdatedAt, out var updated) && (current == null || updated > current))
                current = updated;
        }

        return current;
    }
}
