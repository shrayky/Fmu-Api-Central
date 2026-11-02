using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisIncomingAutoQuenchService : IVetisIncomingAutoQuencher
{
    private readonly IVetisIncomingRepository _incoming;
    private readonly IOrganizationRepository _organizations;
    private readonly VetisIncomingQuenchService _quench;
    private readonly IParametersService _parameters;
    private readonly ILogger<VetisIncomingAutoQuenchService> _logger;
    private readonly TimeProvider _clock;

    public VetisIncomingAutoQuenchService(
        IVetisIncomingRepository incoming,
        IOrganizationRepository organizations,
        VetisIncomingQuenchService quench,
        IParametersService parameters,
        ILogger<VetisIncomingAutoQuenchService> logger,
        TimeProvider? clock = null)
    {
        _incoming = incoming;
        _organizations = organizations;
        _quench = quench;
        _parameters = parameters;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    public Task<VetisQuenchBatch> QuenchBatchAsync(CancellationToken cancellationToken) =>
        QuenchBatchAsync(null, cancellationToken);

    public async Task<VetisQuenchBatch> QuenchBatchAsync(VetisQuenchCursor? cursor, CancellationToken cancellationToken)
    {
        var server = (await _parameters.Current()).VetisSettings ?? new();
        if (!server.Enable)
            return new VetisQuenchBatch { Cursor = cursor ?? new() };

        var batch = server.AutoQuenchBatch();
        var now = _clock.GetUtcNow();
        var organizations = (await _organizations.All())
            .Where(item => item.VetisSettings.Enable && item.VetisSettings.AutoQuench)
            .ToList();
        var lanes = OpenLanes(organizations, cursor);
        var started = 0;
        var looked = 0;
        var quenchedDocuments = 0;
        string? error = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var finished = false;

        // Несозревший документ не входит в число гашений, но один запуск не вычитывает из-за него весь журнал.
        while (started < batch && looked < batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var picked = await NextHead(lanes, batch);
            if (picked.IsFailure)
                return Finish(lanes, quenchedDocuments, picked.Error, false);
            if (picked.Value is not Pick pick)
            {
                finished = true;
                break;
            }

            if (!seen.Add(pick.Head.Id))
                continue;

            var found = await _incoming.Find(pick.Head.Id);
            if (found.IsFailure)
                return Finish(lanes, quenchedDocuments, found.Error, false);
            if (found.Value == null)
            {
                looked++;
                Commit(pick.Lane, pick.Head);
                continue;
            }

            var organization = organizations.First(item => item.Id == found.Value.OrganizationId);
            var due = VetisAutoQuench.DueUuids(
                found.Value.Vsds,
                organization.VetisSettings.DelayHours(),
                now,
                server.RequireVehicleForQuench);
            looked++;
            Commit(pick.Lane, pick.Head);
            if (due.Count == 0)
                continue;

            var quenched = await _quench.QuenchAsync(found.Value.Id, [], cancellationToken, due, VetisQuench.AutoAuthor);
            if (quenched.IsFailure)
            {
                if (quenched.Error == VetisQuench.AlreadyRunning)
                    continue;

                Note(found.Value.Id, quenched.Error);
                started++;
                continue;
            }

            started++;
            if (quenched.Value.Quenched > 0)
                quenchedDocuments++;
            foreach (var item in quenched.Value.Errors)
                Note(found.Value.Id, string.IsNullOrWhiteSpace(item.Message) ? "Ветис не погасил ВСД." : item.Message);
        }

        if (!finished)
        {
            var tail = await PassFinished(lanes, batch);
            if (tail.IsFailure)
                return Finish(lanes, quenchedDocuments, error ?? tail.Error, false);
            finished = tail.Value;
        }

        return Finish(lanes, quenchedDocuments, error, finished);

        void Note(string documentId, string message)
        {
            _logger.LogError("Автогашение документа {DocumentId}: {Error}", documentId, message);
            error ??= message;
        }
    }

    private static void Commit(Lane lane, VetisIncomingHead head)
    {
        lane.ResumeSortAt = head.SortAt;
        lane.ResumeId = head.Id;
    }

    private VetisQuenchBatch Finish(List<Lane> lanes, int quenchedDocuments, string? error, bool finished)
    {
        _logger.LogInformation("Автогашение: погашено документов {Count}", quenchedDocuments);
        return new VetisQuenchBatch
        {
            QuenchedDocuments = quenchedDocuments,
            Error = error ?? "",
            Finished = finished,
            Cursor = Snapshot(lanes)
        };
    }

    private static List<Lane> OpenLanes(
        IReadOnlyList<OrganizationEntity> organizations,
        VetisQuenchCursor? cursor)
    {
        var saved = (cursor?.Lanes ?? [])
            .GroupBy(mark => LaneKey(mark.OrganizationId, mark.Status, mark.Dated))
            .ToDictionary(group => group.Key, group => group.Last());
        var lanes = new List<Lane>();
        foreach (var organization in organizations)
        {
            lanes.Add(Open(organization.Id, VetisIncomingStatus.NotAccepted, true, saved));
            lanes.Add(Open(organization.Id, VetisIncomingStatus.Partial, true, saved));
            lanes.Add(Open(organization.Id, VetisIncomingStatus.NotAccepted, false, saved));
            lanes.Add(Open(organization.Id, VetisIncomingStatus.Partial, false, saved));
        }

        return lanes;
    }

    private static Lane Open(string organizationId, string status, bool dated, Dictionary<string, VetisQuenchLaneMark> saved)
    {
        saved.TryGetValue(LaneKey(organizationId, status, dated), out var mark);
        return new Lane(organizationId, status, dated)
        {
            AfterSortAt = mark?.AfterSortAt ?? 0,
            AfterId = mark?.AfterId ?? "",
            ResumeSortAt = mark?.AfterSortAt ?? 0,
            ResumeId = mark?.AfterId ?? "",
            Exhausted = mark?.Exhausted ?? false
        };
    }

    private static string LaneKey(string organizationId, string status, bool dated) =>
        organizationId + "\n" + status + "\n" + (dated ? "1" : "0");

    private static VetisQuenchCursor Snapshot(List<Lane> lanes) => new()
    {
        Lanes = lanes.Select(lane => new VetisQuenchLaneMark
        {
            OrganizationId = lane.OrganizationId,
            Status = lane.Status,
            Dated = lane.Dated,
            AfterSortAt = lane.ResumeSortAt,
            AfterId = lane.ResumeId,
            Exhausted = lane.Exhausted
        }).ToList()
    };

    private async Task<Result<bool>> PassFinished(List<Lane> lanes, int limit)
    {
        foreach (var lane in lanes)
        {
            if (lane.Exhausted)
                continue;
            if (lane.Index < lane.Buffer.Count)
                return Result.Success(false);

            var filled = await Fill(lane, limit);
            if (filled.IsFailure)
                return Result.Failure<bool>(filled.Error);
            if (!lane.Exhausted)
                return Result.Success(false);
        }

        return Result.Success(true);
    }

    private async Task<Result<Pick?>> NextHead(List<Lane> lanes, int limit)
    {
        Lane? chosen = null;
        VetisIncomingHead head = default;
        foreach (var lane in lanes)
        {
            var filled = await Fill(lane, limit);
            if (filled.IsFailure)
                return Result.Failure<Pick?>(filled.Error);
            if (lane.Index >= lane.Buffer.Count)
                continue;

            var candidate = lane.Buffer[lane.Index];
            if (chosen != null && VetisAutoQuench.CompareDocuments(candidate.SortAt, candidate.Id, head.SortAt, head.Id) >= 0)
                continue;

            chosen = lane;
            head = candidate;
        }

        if (chosen == null)
            return Result.Success<Pick?>(null);

        chosen.Index++;
        return Result.Success<Pick?>(new Pick(chosen, head));
    }

    private async Task<Result> Fill(Lane lane, int limit)
    {
        if (lane.Exhausted || lane.Index < lane.Buffer.Count)
            return Result.Success();

        var page = await _incoming.OldestAfter(
            lane.OrganizationId,
            lane.Status,
            lane.Dated,
            lane.AfterSortAt,
            lane.AfterId,
            limit);
        if (page.IsFailure)
            return Result.Failure(page.Error);

        lane.Buffer = page.Value;
        lane.Index = 0;
        if (page.Value.Count == 0)
        {
            lane.Exhausted = true;
            return Result.Success();
        }

        var last = page.Value[^1];
        lane.AfterSortAt = last.SortAt;
        lane.AfterId = last.Id;
        return Result.Success();
    }

    private sealed class Lane(string organizationId, string status, bool dated)
    {
        public string OrganizationId { get; } = organizationId;

        public string Status { get; } = status;

        public bool Dated { get; } = dated;

        public List<VetisIncomingHead> Buffer { get; set; } = [];

        public int Index { get; set; }

        public long AfterSortAt { get; set; }

        public string AfterId { get; set; } = "";

        public long ResumeSortAt { get; set; }

        public string ResumeId { get; set; } = "";

        public bool Exhausted { get; set; }
    }

    private readonly record struct Pick(Lane Lane, VetisIncomingHead Head);
}
