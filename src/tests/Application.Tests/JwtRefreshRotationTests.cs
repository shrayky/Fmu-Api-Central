using Authentication.Services;
using Domain.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class JwtRefreshRotationTests
{
    /// <summary>
    /// После refresh старый refresh недействителен, новый принимается.
    /// </summary>
    [Fact]
    public void RefreshAccessToken_ротирует_refresh()
    {
        var cache = new RefreshTokenService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RefreshTokenService>.Instance);
        var sut = new JwtTokenService(
            new JwtSettings
            {
                Issuer = "test",
                Audience = "api",
                Key = Convert.ToBase64String(new byte[64]),
                LifetimeMinutes = 60,
                RefreshTokenLifetimeDays = 30
            },
            cache);

        var issued = sut.GenerateTokenPair("admin");
        Assert.True(issued.IsSuccess);

        var refreshed = sut.RefreshAccessToken(issued.Value.RefreshToken);
        Assert.True(refreshed.IsSuccess);
        Assert.NotEqual(issued.Value.RefreshToken, refreshed.Value.RefreshToken);
        Assert.False(cache.IsRefreshTokenValid(issued.Value.RefreshToken));
        Assert.True(cache.IsRefreshTokenValid(refreshed.Value.RefreshToken));
        Assert.Equal("admin", cache.LoginByRefreshToken(refreshed.Value.RefreshToken).Value);
    }
}
