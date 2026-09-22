using CouchDb.Services;
using CouchDB.Driver;
using CouchDB.Driver.Options;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Database.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace CouchDb.Tests;

public class CouchDbHealthServiceTests
{
    /// <summary>
    /// Живое подключение проверяем коротким CheckAvailability, не GetInfoAsync с queryTimeout.
    /// </summary>
    [Fact]
    public async Task IsConnectionHealthy_при_enable_зовёт_CheckAvailability()
    {
        var status = new FakeDbStatus { Available = false };
        var sut = CreateSut(
            new FakeParameters
            {
                CurrentValue =
                {
                    DatabaseConnection =
                    {
                        Enable = true,
                        NetAddress = "http://localhost:5984"
                    }
                }
            },
            status);

        Assert.False(await sut.IsConnectionHealthy());
        Assert.Equal(1, status.AvailabilityCalls);
        Assert.Equal("http://localhost:5984", status.LastUrl);
    }

    private static CouchDbHealthService CreateSut(IParametersService parameters, IDbStatusService status)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(50) };
        var client = new CouchClient(
            "http://127.0.0.1:1",
            new BasicCredentials("n", "n"),
            new CouchClientOptions { HttpClient = httpClient, ThrowOnQueryWarning = false });

        return new CouchDbHealthService(
            new Context(client),
            parameters,
            NullLogger<CouchDbHealthService>.Instance,
            status);
    }

    private sealed class FakeParameters : IParametersService
    {
        public Parameters CurrentValue { get; } = new();
        public Task<Parameters> Current() => Task.FromResult(CurrentValue);
        public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
    }

    private sealed class FakeDbStatus : IDbStatusService
    {
        public int AvailabilityCalls { get; private set; }
        public string? LastUrl { get; private set; }
        public bool Available { get; set; }

        public Task<bool> CheckAvailability(string databaseUrl, CancellationToken cancellationToken = default)
        {
            AvailabilityCalls++;
            LastUrl = databaseUrl;
            return Task.FromResult(Available);
        }

        public Task<bool> EnsureDatabasesExists(DatabaseConnection connection, string[] databasesNames, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<bool> EnsureDefaultUserExists(CancellationToken cancellationToken)
            => Task.FromResult(true);
    }
}
