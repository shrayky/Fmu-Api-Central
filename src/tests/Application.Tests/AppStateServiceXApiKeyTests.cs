using Application.AppState;

namespace Application.Tests;

public class AppStateServiceXApiKeyTests
{
    /// <summary>
    /// Истёкший ключ CDN кэш отдаёт пустым, чтобы воркер запросил новый.
    /// </summary>
    [Fact]
    public void XApiKey_истекший_пустой()
    {
        var state = new AppStateService();
        var inn = "7701234567";
        state.UpdateXApiKey(inn, "cdn-key", DateTime.Now.AddMinutes(-1));

        var key = state.XApiKey(inn);

        Assert.Equal(string.Empty, key.Token);
    }

    [Fact]
    public void XApiKey_живой_возвращается()
    {
        var state = new AppStateService();
        var until = DateTime.Now.AddHours(1);
        state.UpdateXApiKey("7701234567", "cdn-key", until);

        var key = state.XApiKey("7701234567");

        Assert.Equal("cdn-key", key.Token);
        Assert.Equal(until, key.LiveUntil);
        Assert.Equal("7701234567", Assert.Single(state.XApiKeys()).Inn);
    }

    /// <summary>
    /// Пауза x-api-key живёт в памяти процесса и не смешивается между ИНН.
    /// </summary>
    [Fact]
    public void PostponeXApiKey_помнит_срок_только_своего_инн()
    {
        var state = new AppStateService();
        var until = new DateTime(2026, 11, 5, 4, 0, 0, DateTimeKind.Local);

        Assert.Null(state.XApiKeyPostponedUntil("7701234567"));

        state.PostponeXApiKey("7701234567", until);

        Assert.Equal(until, state.XApiKeyPostponedUntil("7701234567"));
        Assert.Null(state.XApiKeyPostponedUntil("7707654321"));
    }

    [Fact]
    public void ClearXApiKeyPostpone_снимает_паузу()
    {
        var state = new AppStateService();
        var until = new DateTime(2026, 11, 5, 4, 0, 0, DateTimeKind.Local);
        state.PostponeXApiKey("7701234567", until);

        state.ClearXApiKeyPostpone("7701234567");

        Assert.Null(state.XApiKeyPostponedUntil("7701234567"));
    }
}
