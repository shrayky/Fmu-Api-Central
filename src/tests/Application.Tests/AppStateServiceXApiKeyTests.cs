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
}
