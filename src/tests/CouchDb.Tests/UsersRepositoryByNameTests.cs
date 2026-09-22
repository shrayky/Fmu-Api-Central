using CouchDb.Repositories;
using Domain.AppState.Interfaces;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.TrueApiIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using CouchDB.Driver;
using CouchDB.Driver.Options;

namespace CouchDb.Tests;

public class UsersRepositoryByNameTests
{
    /// <summary>
    /// Нет живого подключения — ByName не ходит в CouchDB.
    /// </summary>
    [Fact]
    public async Task ByName_без_подключения_возвращает_недоступна()
    {
        var sut = CreateSut(dbOnline: false);

        var result = await sut.ByName("admin");

        Assert.True(result.IsFailure);
        Assert.Equal("БД недоступна сейчас", result.Error);
    }

    private static UsersRepository CreateSut(bool dbOnline)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(50) };
        var client = new CouchClient(
            "http://127.0.0.1:1",
            new BasicCredentials("n", "n"),
            new CouchClientOptions { HttpClient = httpClient, ThrowOnQueryWarning = false });

        var services = new ServiceCollection();
        services.AddSingleton(new Context(client));
        services.AddSingleton<IParametersService>(new FakeParameters());
        services.AddSingleton<IApplicationState>(new FakeAppState(dbOnline));
        services.AddSingleton(NullLogger<Domain.Entitys.UserEntity>.Instance);
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));

        return new UsersRepository(services.BuildServiceProvider());
    }

    private sealed class FakeParameters : IParametersService
    {
        public Task<Parameters> Current() => Task.FromResult(new Parameters());
        public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
    }

    private sealed class FakeAppState(bool dbOnline) : IApplicationState
    {
        public bool DbState() => dbOnline;
        public void DbStateUpdate(bool isOnline) { }
        public bool NeedRestart() => false;
        public void UpdateNeedRestart(bool need) { }
        public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil) { }
        public TrueApiToken TrueApiToken(string inn) => new();
        public IReadOnlyList<TrueApiToken> TrueApiTokens() => [];
        public void MarkGisMtPushPending() { }
        public bool GisMtPushPending() => false;
        public void ClearGisMtPushPending() { }
    }
}
