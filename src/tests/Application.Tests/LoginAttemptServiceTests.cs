using Authentication.Services;
using Domain.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Tests;

public class LoginAttemptServiceTests
{
    /// <summary>
    /// Пятая неудача блокирует логин.
    /// </summary>
    [Fact]
    public void RegisterFailure_пятая_попытка_блокирует_логин()
    {
        var sut = CreateSut();

        for (var i = 0; i < 4; i++)
            sut.RegisterFailure("admin");

        Assert.False(sut.IsLocked("admin"));

        sut.RegisterFailure("admin");

        Assert.True(sut.IsLocked("admin"));
    }

    /// <summary>
    /// Успешный вход сбрасывает счётчик неудач.
    /// </summary>
    [Fact]
    public void RegisterSuccess_сбрасывает_счётчик()
    {
        var sut = CreateSut();

        for (var i = 0; i < 4; i++)
            sut.RegisterFailure("admin");

        sut.RegisterSuccess("admin");
        sut.RegisterFailure("admin");

        Assert.False(sut.IsLocked("admin"));
    }

    /// <summary>
    /// Блокировка не обходится сменой регистра имени.
    /// </summary>
    [Fact]
    public void IsLocked_не_зависит_от_регистра()
    {
        var sut = CreateSut();

        for (var i = 0; i < 5; i++)
            sut.RegisterFailure("Admin");

        Assert.True(sut.IsLocked("admin"));
    }

    /// <summary>
    /// Разные логины считаются отдельно.
    /// </summary>
    [Fact]
    public void RegisterFailure_не_блокирует_другой_логин()
    {
        var sut = CreateSut();

        for (var i = 0; i < 5; i++)
            sut.RegisterFailure("admin");

        Assert.False(sut.IsLocked("manager"));
    }

    /// <summary>
    /// После истечения блокировки логин снова доступен.
    /// </summary>
    [Fact]
    public void IsLocked_снимается_после_истечения()
    {
        var time = new FakeTimeProvider();
        var sut = new LoginAttemptService(
            new MemoryCache(new MemoryCacheOptions()),
            time,
            new LoginAttemptOptions
            {
                MaxFailedAttempts = 5,
                LockoutDuration = TimeSpan.FromMinutes(15)
            });

        for (var i = 0; i < 5; i++)
            sut.RegisterFailure("admin");

        time.UtcNow = time.UtcNow.AddMinutes(15).AddSeconds(1);

        Assert.False(sut.IsLocked("admin"));
    }

    private static LoginAttemptService CreateSut()
        => new(
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            new LoginAttemptOptions());

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
