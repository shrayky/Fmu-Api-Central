using Authentication.Services;
using Domain.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Tests;

public class AgentNonceServiceTests
{
    /// <summary>
    /// Повтор того же nonce в окне отклоняется.
    /// </summary>
    [Fact]
    public void Consume_отклоняет_повтор_nonce()
    {
        var time = new FakeTimeProvider();
        var sut = CreateSut(time);
        var timestamp = time.GetUtcNow().ToUnixTimeSeconds();

        Assert.True(sut.Consume("nonce-1", timestamp).IsSuccess);
        Assert.True(sut.Consume("nonce-1", timestamp).IsFailure);
    }

    /// <summary>
    /// Метка старше окна не принимается.
    /// </summary>
    [Fact]
    public void Consume_отклоняет_старую_метку()
    {
        var time = new FakeTimeProvider();
        var sut = CreateSut(time);
        var timestamp = time.GetUtcNow().AddMinutes(-6).ToUnixTimeSeconds();

        Assert.True(sut.Consume("nonce-1", timestamp).IsFailure);
    }

    /// <summary>
    /// Разные nonce проходят.
    /// </summary>
    [Fact]
    public void Consume_принимает_разные_nonce()
    {
        var time = new FakeTimeProvider();
        var sut = CreateSut(time);
        var timestamp = time.GetUtcNow().ToUnixTimeSeconds();

        Assert.True(sut.Consume("a", timestamp).IsSuccess);
        Assert.True(sut.Consume("b", timestamp).IsSuccess);
    }

    private static AgentNonceService CreateSut(TimeProvider time)
        => new(
            new MemoryCache(new MemoryCacheOptions()),
            time,
            new AgentNonceOptions());

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
