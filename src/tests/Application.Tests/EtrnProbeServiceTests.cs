using Application.Etrn;
using Application.Tests.Fakes;
using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class EtrnProbeServiceTests
{
    [Fact]
    public async Task Пустые_секреты_ОФД_берутся_у_того_же_подключения()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization();

        var result = await world.Service.CheckAsync("o1", new EtrnProbeRequest
        {
            Id = "ofd-1",
            Type = EtrnOperatorType.Ofd,
            Name = " Склад ",
            SubscriberId = " 2775225 ",
            ApiKey = "",
            ClientId = " new-client ",
            ClientSecret = SecretMask.Placeholder,
            KeycloakUrl = "",
            Realm = "",
            ApiUrl = " https://api.example/ "
        }, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.True(result.Value.Ok);
        var connection = world.Ofd.Last!.Connection;
        Assert.Equal("ofd-secret", connection.ApiKey);
        Assert.Equal("client-secret", connection.ClientSecret);
        Assert.Equal("new-client", connection.ClientId);
        Assert.Equal("2775225", connection.SubscriberId);
        Assert.Equal("", connection.KeycloakUrl);
        Assert.Equal("", connection.Realm);
        Assert.Equal("https://api.example", connection.ApiUrl);
        Assert.Equal("Склад", connection.Name);
        Assert.Equal(0, world.Organizations.Updates);
    }

    [Fact]
    public async Task Новое_подключение_не_получает_чужой_секрет()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization();

        await world.Service.CheckAsync("o1", new EtrnProbeRequest
        {
            Id = "ofd-2",
            Type = EtrnOperatorType.Ofd,
            SubscriberId = "1",
            ApiKey = "",
            ClientId = "client",
            ClientSecret = ""
        }, CancellationToken.None);

        Assert.Equal("", world.Ofd.Last!.Connection.ApiKey);
        Assert.Equal("", world.Ofd.Last.Connection.ClientSecret);
    }

    [Fact]
    public async Task Saby_берёт_сертификат_с_формы_и_не_хранит_секреты_ОФД()
    {
        var world = new World();
        world.Organizations.Store["o1"] = Organization();

        await world.Service.CheckAsync("o1", new EtrnProbeRequest
        {
            Id = "c1",
            Type = EtrnOperatorType.Saby,
            ApiKey = "не должен сохраниться",
            ClientSecret = "тоже",
            CertificateNumber = " serial-form ",
            CertificatePassword = ""
        }, CancellationToken.None);

        Assert.Equal(1, world.Saby.Calls);
        Assert.Equal(0, world.Ofd.Calls);
        Assert.Equal("", world.Saby.Last!.Connection.ApiKey);
        Assert.Equal("", world.Saby.Last.Connection.ClientSecret);
        Assert.Equal("serial-form", world.Saby.Last.CertificateNumber);
        Assert.Equal("pwd", world.Saby.Last.CertificatePassword);
    }

    [Fact]
    public async Task Проверка_пишет_в_журнал_только_служебный_вызов()
    {
        var world = new World();
        world.Ofd.Next = new EtrnProbeResult
        {
            Ok = false,
            Message = "ОФД ответила 401",
            Exchanges =
            [
                new EtrnExchange { Method = EtrnMethods.AuthByCertificate, Completed = true, RequestBody = "секрет" },
                new EtrnExchange { Method = "Проверка подключения", Completed = false, Error = "401" }
            ]
        };

        var result = await world.Service.CheckAsync("o1", new EtrnProbeRequest
        {
            Id = "ofd-2",
            Type = EtrnOperatorType.Ofd,
            SubscriberId = "1",
            ApiKey = "key",
            ClientId = "client"
        }, CancellationToken.None);

        Assert.False(result.Value.Ok);
        Assert.Equal("ОФД ответила 401", result.Value.Message);
        var row = Assert.Single(world.Requests.Store.Values);
        Assert.Equal("Проверка подключения", row.Method);
        Assert.Equal("o1", row.OrganizationId);
        Assert.Equal("ofd-2", row.Operator.ConnectionId);
        Assert.Equal(0, world.Organizations.Updates);
    }

    [Fact]
    public async Task Пустой_тип_не_вызывает_оператора()
    {
        var world = new World();

        var result = await world.Service.CheckAsync("o1", new EtrnProbeRequest(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Выберите подключение", result.Error);
        Assert.Equal(0, world.Saby.Calls);
        Assert.Equal(0, world.Ofd.Calls);
    }

    private static OrganizationEntity Organization() => new()
    {
        Id = "o1",
        Name = "Организация",
        Inn = "7700000000",
        TrueApiIntegrationSettings = new TrueApiIntegrationSettings
        {
            DigitalSignature = "serial-stored",
            Password = "pwd"
        },
        EtrnConnections =
        [
            new EtrnConnection
            {
                Id = "ofd-1",
                Type = EtrnOperatorType.Ofd,
                ApiKey = "ofd-secret",
                ClientSecret = "client-secret"
            }
        ]
    };

    private sealed class World
    {
        public MemoryOrganizationRepository Organizations { get; } = new();

        public MemoryEtrnRequests Requests { get; } = new();

        public ProbeOperator Saby { get; } = new() { Type = EtrnOperatorType.Saby };

        public ProbeOperator Ofd { get; } = new() { Type = EtrnOperatorType.Ofd };

        public EtrnProbeService Service { get; }

        public World()
        {
            var recorder = new EtrnRequestRecorder(Requests, NullLogger<EtrnRequestRecorder>.Instance);
            Service = new EtrnProbeService([Saby, Ofd], Organizations, recorder);
        }
    }

    private sealed class ProbeOperator : IEtrnOperator
    {
        public string Type { get; init; } = "";

        public int Calls { get; private set; }

        public EtrnOperatorContext? Last { get; private set; }

        public EtrnProbeResult Next { get; set; } = new() { Ok = true, Message = "ок" };

        public Task<Result<EtrnProbeResult>> ProbeAsync(EtrnOperatorContext context, CancellationToken cancellationToken)
        {
            Calls++;
            Last = context;
            return Task.FromResult(Result.Success(Next));
        }

        public Task<Result<EtrnFetchResult>> FetchAsync(EtrnOperatorContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<EtrnExecuteResult>> ExecuteAsync(
            EtrnOperatorContext context,
            EtrnExecuteCommand command,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<EtrnStageResult>> ReadStageAsync(
            EtrnOperatorContext context,
            string operatorDocumentId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
