using Domain.Entitys.Instance;

namespace Domain.Authentication;

public static class LegacyAgentApiPolicy
{
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromDays(7);

    public static bool ShouldDisable(IReadOnlyList<InstanceEntity> instances, DateTime utcNow)
    {
        var connected = instances
            .Where(instance => instance.HandshakeAtUtc != null || instance.LastLegacyAccessUtc != null)
            .ToList();

        if (connected.Count == 0)
            return false;

        if (connected.Any(instance => instance.HandshakeAtUtc == null))
            return false;

        if (connected.Any(instance =>
                instance.LastLegacyAccessUtc != null
                && utcNow - instance.LastLegacyAccessUtc.Value < QuietPeriod))
            return false;

        return true;
    }
}
