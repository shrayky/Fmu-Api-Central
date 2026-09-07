using Domain.Configuration;

namespace Domain.Tests;

public class CorsOriginsTests
{
    /// <summary>
    /// Без настроек остаются только локальные origin WebApp.
    /// </summary>
    [Fact]
    public void Resolve_всегда_включает_localhost_и_loopback()
    {
        var origins = CorsOrigins.Resolve(null);

        Assert.Contains("http://localhost:2580", origins);
        Assert.Contains("http://127.0.0.1:2580", origins);
        Assert.Equal(2, origins.Length);
    }

    /// <summary>
    /// LAN и Caddy добавляются, хвост '/' и пустые строки отбрасываются.
    /// </summary>
    [Fact]
    public void Resolve_добавляет_настроенные_origin()
    {
        var origins = CorsOrigins.Resolve(
        [
            "  http://192.168.1.10:2580/  ",
            "",
            "https://central.example",
            "not-a-url",
            "http://localhost:2580"
        ]);

        Assert.Contains("http://localhost:2580", origins);
        Assert.Contains("http://127.0.0.1:2580", origins);
        Assert.Contains("http://192.168.1.10:2580", origins);
        Assert.Contains("https://central.example", origins);
        Assert.DoesNotContain("not-a-url", origins);
        Assert.Equal(4, origins.Length);
    }
}
