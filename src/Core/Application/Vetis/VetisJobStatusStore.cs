using Domain.Attributes;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Vetis;

/// <summary>
/// Живёт в памяти процесса: воркер и вкладка «Фоновые задания» не делят один запрос.
/// </summary>
[AutoRegisterService(ServiceLifetime.Singleton)]
public sealed class VetisJobStatusStore : IVetisJobStatusStore
{
    private readonly object _gate = new();

    private readonly Dictionary<string, Slot> _jobs = new(StringComparer.Ordinal);

    public void Report(VetisJobUpdate update)
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

    public IReadOnlyList<VetisJobStatusLine> Current()
    {
        lock (_gate)
        {
            return _jobs.Values
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Title, StringComparer.Ordinal)
                .Select(item => new VetisJobStatusLine
                {
                    Id = item.Id,
                    Title = item.Title,
                    Text = VetisJobStatusText.Format(item.Phase, item.CountLabel, item.Count, item.At, item.NextAt, item.Error)
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

        public VetisJobPhase Phase { get; init; }

        public DateTimeOffset At { get; init; }

        public DateTimeOffset? NextAt { get; init; }

        public int? Count { get; init; }

        public string Error { get; init; } = "";
    }
}
