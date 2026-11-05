using Domain.Attributes;
using Domain.Etrn;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Etrn;

/// <summary>
/// Живёт в памяти процесса: воркер и вкладка «Фоновые задания» не делят один запрос.
/// </summary>
[AutoRegisterService(ServiceLifetime.Singleton)]
public sealed class EtrnJobStatusStore : IEtrnJobStatusStore
{
    private readonly object _gate = new();

    private readonly Dictionary<string, Slot> _jobs = new(StringComparer.Ordinal);

    public void Report(EtrnJobUpdate update)
    {
        lock (_gate)
        {
            _jobs.TryGetValue(update.Id, out var previous);
            _jobs[update.Id] = new Slot
            {
                Id = update.Id,
                Title = update.Title,
                CountLabel = update.CountLabel,
                Order = update.Order,
                Phase = update.Phase,
                At = update.At,
                NextAt = update.NextAt,
                Count = update.Count ?? previous?.Count,
                Error = update.Error ?? ""
            };
        }
    }

    public IReadOnlyList<EtrnJobStatusLine> Current()
    {
        lock (_gate)
        {
            return _jobs.Values
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Title, StringComparer.Ordinal)
                .Select(item => new EtrnJobStatusLine
                {
                    Id = item.Id,
                    Title = item.Title,
                    Text = EtrnJobStatusText.Format(item.Phase, item.CountLabel, item.Count, item.At, item.NextAt, item.Error)
                })
                .ToList();
        }
    }

    private sealed class Slot
    {
        public string Id { get; init; } = "";

        public string Title { get; init; } = "";

        public string CountLabel { get; init; } = "";

        public int Order { get; init; }

        public EtrnJobPhase Phase { get; init; }

        public DateTimeOffset At { get; init; }

        public DateTimeOffset? NextAt { get; init; }

        public int? Count { get; init; }

        public string Error { get; init; } = "";
    }
}
