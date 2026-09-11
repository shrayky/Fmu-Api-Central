using Domain.Entitys.Organization;
using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class TrueApiExchangeTokensTests
{
    /// <summary>
    /// Без списка организаций группы токен в сеанс не отдаём.
    /// </summary>
    [Fact]
    public void ForExchange_без_списка_группы_пусто()
    {
        var organizations = new[]
        {
            Org("o1", "7701234567")
        };
        var tokens = new[]
        {
            Token("7701234567", "live-token", new DateTime(2026, 9, 12, 10, 0, 0))
        };

        var result = TrueApiExchangeTokens.ForExchange(organizations, tokens, []);

        Assert.Empty(result);
    }

    /// <summary>
    /// В сеанс попадают только токены организаций из списка группы.
    /// </summary>
    [Fact]
    public void ForExchange_только_организации_группы()
    {
        var expired = new DateTime(2026, 9, 12, 10, 0, 0);
        var organizations = new[]
        {
            Org("o1", "7701234567"),
            Org("o2", "7707654321")
        };
        var tokens = new[]
        {
            Token("7701234567", "token-1", expired),
            Token("7707654321", "token-2", expired)
        };

        var result = TrueApiExchangeTokens.ForExchange(organizations, tokens, ["o1"]);

        var item = Assert.Single(result);
        Assert.Equal("7701234567", item.Inn);
        Assert.Equal("token-1", item.Token);
        Assert.Equal(expired, item.Expired);
    }

    /// <summary>
    /// Организация в группе есть, но токена нет — в пакет не кладём.
    /// </summary>
    [Fact]
    public void ForExchange_без_токена_пусто()
    {
        var organizations = new[]
        {
            Org("o1", "7701234567")
        };

        var result = TrueApiExchangeTokens.ForExchange(organizations, [], ["o1"]);

        Assert.Empty(result);
    }

    private static OrganizationEntity Org(string id, string inn)
        => new()
        {
            Id = id,
            Inn = inn
        };

    private static TrueApiToken Token(string inn, string token, DateTime liveUntil)
        => new()
        {
            Inn = inn,
            Token = token,
            LiveUntil = liveUntil
        };
}
