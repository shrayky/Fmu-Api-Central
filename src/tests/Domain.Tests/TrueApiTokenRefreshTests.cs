using Domain.Entitys.Organization;
using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class TrueApiTokenRefreshTests
{
    /// <summary>
    /// Живой UUID в simpleSignIn не обновляется, пустой — берётся.
    /// </summary>
    [Fact]
    public void Due_живой_uuid_пропускает_пустой_берёт()
    {
        var organizations = new[]
        {
            Org("live", "7700000001", enabled: true),
            Org("empty", "7700000002", enabled: true)
        };

        var result = TrueApiTokenRefresh.Due(organizations, inn => inn == "7700000001"
            ? new TrueApiToken { Inn = inn, Token = "uuid", LiveUntil = DateTime.Now.AddHours(1) }
            : new TrueApiToken());

        var due = Assert.Single(result);
        Assert.Equal("empty", due.Id);
    }

    /// <summary>
    /// Пустой ИНН и выключенная интеграция в цикл авторизации не входят.
    /// </summary>
    [Fact]
    public void Due_без_инн_или_интеграции_пропускает()
    {
        var organizations = new[]
        {
            Org("no-inn", "", enabled: true),
            Org("off", "7700000003", enabled: false)
        };

        var result = TrueApiTokenRefresh.Due(organizations, _ => new TrueApiToken());

        Assert.Empty(result);
    }

    /// <summary>
    /// Ключ запрашивается при живом UUID: в список входят включённые организации с сертификатом.
    /// </summary>
    [Fact]
    public void ForXApiKey_берёт_включённую_с_сертификатом()
    {
        var organizations = new[]
        {
            Org("ready", "7700000001", enabled: true, signature: "serial"),
            Org("off", "7700000002", enabled: false, signature: "serial"),
            Org("no-cert", "7700000003", enabled: true, signature: "  "),
            new OrganizationEntity { Id = "no-settings", Inn = "7700000004", TrueApiIntegrationSettings = null! }
        };

        var result = TrueApiTokenRefresh.ForXApiKey(organizations);

        var only = Assert.Single(result);
        Assert.Equal("ready", only.Id);
    }

    private static OrganizationEntity Org(string id, string inn, bool enabled, string signature = "")
        => new()
        {
            Id = id,
            Inn = inn,
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                Enable = enabled,
                DigitalSignature = signature
            }
        };
}
