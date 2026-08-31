namespace Domain.Entitys.InstanceGroup;

/// <summary>
/// Решает, можно ли отдавать инстансу пакет автообновления по флагу группы.
/// </summary>
public static class AutoUpdatePolicy
{
    public static bool IsAllowed(InstanceGroupEntity? group) =>
        group is { AutoUpdateAllowed: true };
}
