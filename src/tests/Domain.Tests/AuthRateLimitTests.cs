using System.Net;
using Domain.Authentication;

namespace Domain.Tests;

public class AuthRateLimitTests
{
    /// <summary>
    /// Разные клиенты не делят ключ. Без IP — общий unknown, не склеиваем с реальным адресом.
    /// </summary>
    [Fact]
    public void PartitionKey_различает_клиентов()
    {
        var a = AuthRateLimit.PartitionKey(IPAddress.Parse("192.168.1.10"));
        var b = AuthRateLimit.PartitionKey(IPAddress.Parse("192.168.1.11"));
        var empty = AuthRateLimit.PartitionKey(null);

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, empty);
        Assert.Equal("unknown", empty);
    }

    /// <summary>
    /// Login, refresh и смена пароля — разные политики, один клиент не закрывает все сразу.
    /// </summary>
    [Fact]
    public void PolicyNames_не_совпадают()
    {
        Assert.NotEqual(AuthRateLimit.Login, AuthRateLimit.Refresh);
        Assert.NotEqual(AuthRateLimit.Login, AuthRateLimit.ChangePassword);
        Assert.NotEqual(AuthRateLimit.Refresh, AuthRateLimit.ChangePassword);
    }
}
