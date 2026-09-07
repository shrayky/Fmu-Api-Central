using Domain.Authentication;
using Domain.Entitys.Instance;

namespace Domain.Tests;

public class LegacyAgentApiPolicyTests
{
    /// <summary>
    /// Пока никто не сделал handshake, старый API не отключаем.
    /// </summary>
    [Fact]
    public void ShouldDisable_false_если_нет_handshake()
    {
        var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        var instances = new[]
        {
            new InstanceEntity { LastLegacyAccessUtc = now.AddDays(-1) }
        };

        Assert.False(LegacyAgentApiPolicy.ShouldDisable(instances, now));
    }

    /// <summary>
    /// Узел только на старом протоколе блокирует автоотключение.
    /// </summary>
    [Fact]
    public void ShouldDisable_false_если_есть_узел_без_handshake()
    {
        var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        var instances = new[]
        {
            new InstanceEntity { HandshakeAtUtc = now.AddDays(-10) },
            new InstanceEntity { LastLegacyAccessUtc = now.AddDays(-1) }
        };

        Assert.False(LegacyAgentApiPolicy.ShouldDisable(instances, now));
    }

    /// <summary>
    /// Все перешли, legacy тихий 7 дней — можно закрыть.
    /// </summary>
    [Fact]
    public void ShouldDisable_true_когда_все_на_handshake_и_legacy_тих()
    {
        var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        var instances = new[]
        {
            new InstanceEntity
            {
                HandshakeAtUtc = now.AddDays(-10),
                LastLegacyAccessUtc = now.AddDays(-8)
            },
            new InstanceEntity { HandshakeAtUtc = now.AddDays(-3) }
        };

        Assert.True(LegacyAgentApiPolicy.ShouldDisable(instances, now));
    }

    /// <summary>
    /// Узел из UI, который ни разу не выходил, не мешает.
    /// </summary>
    [Fact]
    public void ShouldDisable_игнорирует_узлы_без_обращений()
    {
        var now = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        var instances = new[]
        {
            new InstanceEntity(),
            new InstanceEntity { HandshakeAtUtc = now.AddDays(-10) }
        };

        Assert.True(LegacyAgentApiPolicy.ShouldDisable(instances, now));
    }
}
