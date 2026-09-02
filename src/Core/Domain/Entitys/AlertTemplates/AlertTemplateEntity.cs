using Domain.Entitys.Interfaces;

namespace Domain.Entitys.AlertTemplates;

public class AlertTemplateEntity : IHaveStringId
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public List<AlertTemplateScheduleSlot> Scheduler { get; set; } = [];

    public bool IsDueAt(DateTime now)
    {
        if (!Enabled || Scheduler.Count == 0)
            return false;

        foreach (var slot in Scheduler)
        {
            if (!TimeOnly.TryParse(slot.Time, out var time))
                continue;

            if (time.Hour == now.Hour && time.Minute == now.Minute)
                return true;
        }

        return false;
    }
}
