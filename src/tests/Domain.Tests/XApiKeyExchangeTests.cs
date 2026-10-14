using Domain.Entitys.Organization;
using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class XApiKeyExchangeTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Local);

    /// <summary>
    /// Без списка организаций группы ключ в сеанс не отдаём.
    /// </summary>
    [Fact]
    public void ForExchange_без_списка_группы_пусто()
    {
        var organizations = new[]
        {
            Org("o1", "7701234567", "serial", Now.AddHours(1))
        };
        var keys = new[]
        {
            Key("7701234567", "cdn-key", Now.AddHours(1))
        };

        var result = XApiKeyExchange.ForExchange(organizations, keys, [], Now);

        Assert.Empty(result);
    }

    /// <summary>
    /// Живой ключ уходит только организации группы, у которой выбран сертификат.
    /// </summary>
    [Fact]
    public void ForExchange_живой_ключ_только_своей_организации_с_эцп()
    {
        var until = Now.AddHours(1);
        var organizations = new[]
        {
            Org("o1", "7701234567", "serial", until),
            Org("o2", "7707654321", "serial", until),
            Org("o3", "7700000000", "", until)
        };
        var keys = new[]
        {
            Key("7701234567", "key-1", until),
            Key("7707654321", "key-2", until),
            Key("7700000000", "key-3", until)
        };

        var result = XApiKeyExchange.ForExchange(organizations, keys, ["o1"], Now);

        var item = Assert.Single(result);
        Assert.Equal("7701234567", item.Inn);
        Assert.Equal("key-1", item.XApiKey);
        Assert.False(item.XApiKeyExpired);
        Assert.Equal(until, item.ExpiresAt);
    }

    /// <summary>
    /// Ключ ещё ни разу не получали — в пакет не кладём.
    /// </summary>
    [Fact]
    public void ForExchange_без_даты_и_ключа_пропуск()
    {
        var organizations = new[]
        {
            Org("o1", "7701234567", "serial", null)
        };

        var result = XApiKeyExchange.ForExchange(organizations, [], ["o1"], Now);

        Assert.Empty(result);
    }

    /// <summary>
    /// После перезапуска дата ещё в будущем, а ключа в памяти нет — узел оставляет прежний ключ.
    /// </summary>
    [Fact]
    public void ForExchange_будущая_дата_без_ключа_в_памяти_пропуск()
    {
        var organizations = new[]
        {
            Org("o1", "7701234567", "serial", Now.AddHours(1))
        };

        var result = XApiKeyExchange.ForExchange(organizations, [], ["o1"], Now);

        Assert.Empty(result);
    }

    /// <summary>
    /// Истёкший срок: пустой ключ и признак, чтобы узел перестал его использовать.
    /// </summary>
    [Fact]
    public void ForExchange_истекший_срок_признак_без_ключа()
    {
        var until = Now.AddMinutes(-1);
        var organizations = new[]
        {
            Org("o1", "7701234567", "serial", until)
        };
        var keys = new[]
        {
            Key("7701234567", "stale-key", until)
        };

        var result = XApiKeyExchange.ForExchange(organizations, keys, ["o1"], Now);

        var item = Assert.Single(result);
        Assert.Equal("7701234567", item.Inn);
        Assert.Equal(string.Empty, item.XApiKey);
        Assert.True(item.XApiKeyExpired);
        Assert.Equal(until, item.ExpiresAt);
    }

    /// <summary>
    /// Срок в базе мог не сохраниться: живой ключ в памяти важнее просроченной даты.
    /// </summary>
    [Fact]
    public void ForExchange_живой_ключ_важнее_просроченной_даты_в_базе()
    {
        var stale = Now.AddMinutes(-1);
        var liveUntil = Now.AddHours(1);
        var organizations = new[]
        {
            Org("o1", "7701234567", "serial", stale)
        };
        var keys = new[]
        {
            Key("7701234567", "fresh-key", liveUntil)
        };

        var result = XApiKeyExchange.ForExchange(organizations, keys, ["o1"], Now);

        var item = Assert.Single(result);
        Assert.Equal("7701234567", item.Inn);
        Assert.Equal("fresh-key", item.XApiKey);
        Assert.False(item.XApiKeyExpired);
        Assert.Equal(liveUntil, item.ExpiresAt);
    }

    private static OrganizationEntity Org(string id, string inn, string signature, DateTime? expiresAt)
        => new()
        {
            Id = id,
            Inn = inn,
            XApiKeyExpiresAt = expiresAt,
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                DigitalSignature = signature
            }
        };

    private static TrueApiToken Key(string inn, string token, DateTime liveUntil)
        => new()
        {
            Inn = inn,
            Token = token,
            LiveUntil = liveUntil
        };
}
