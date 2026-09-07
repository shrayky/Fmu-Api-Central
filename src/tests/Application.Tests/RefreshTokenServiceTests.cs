using Authentication.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class RefreshTokenServiceTests
{
    /// <summary>
    /// Logout должен снимать тот же ключ, которым токен сохранили.
    /// </summary>
    [Fact]
    public void Remove_отзывает_сохранённый_refresh()
    {
        var sut = new RefreshTokenService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RefreshTokenService>.Instance);

        sut.SaveRefreshToken("tok-1", "admin", DateTime.UtcNow.AddDays(1));
        Assert.True(sut.IsRefreshTokenValid("tok-1"));

        sut.RemoveRefreshToken("tok-1");

        Assert.False(sut.IsRefreshTokenValid("tok-1"));
        Assert.True(sut.LoginByRefreshToken("tok-1").IsFailure);
    }
}
