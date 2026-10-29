using Application.Tests.Fakes;
using Application.Vetis;
using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Configuration.Options;
using Domain.Dto.Responces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class VetisProbeServiceTests
{
    /// <summary>
    /// Выключенный флажок не пускает вызов в шлюз.
    /// </summary>
    [Fact]
    public async Task Check_отказывает_когда_Ветис_выключен()
    {
        var gateway = new FakeVetisGateway();
        var sut = Create(gateway, enable: false);

        var result = await sut.CheckAsync("o1", ReadyRequest(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ветис API выключен в настройках сервера.", result.Error);
        Assert.Equal(0, gateway.Checks);
    }

    /// <summary>
    /// Пустой пароль в форме берётся из карточки организации.
    /// </summary>
    [Fact]
    public async Task Check_подставляет_сохранённый_пароль()
    {
        var gateway = new FakeVetisGateway();
        var organizations = new MemoryOrganizations();
        organizations.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            VetisSettings = new OrganizationVetisSettings { BasicPassword = "stored-secret" }
        };
        var sut = Create(gateway, organizations);

        var request = ReadyRequest();
        request.BasicPassword = "";
        var result = await sut.CheckAsync("o1", request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Ok);
        Assert.Equal("stored-secret", gateway.LastCall?.BasicPassword);
        Assert.Equal(VetisEndpoints.ProductionApplicationManagement, gateway.LastCall?.Endpoint);
    }

    /// <summary>
    /// Маска и пустой API-ключ в форме берутся из карточки организации.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(SecretMask.Placeholder)]
    public async Task Check_подставляет_сохранённый_api_ключ(string incomingKey)
    {
        var gateway = new FakeVetisGateway();
        var organizations = new MemoryOrganizations();
        organizations.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            VetisSettings = new OrganizationVetisSettings { ApiKey = "stored-key", BasicPassword = "stored-secret" }
        };
        var sut = Create(gateway, organizations);

        var request = ReadyRequest();
        request.ApiKey = incomingKey;
        request.BasicPassword = "";
        var result = await sut.CheckAsync("o1", request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("stored-key", gateway.LastCall?.ApiKey);
    }

    /// <summary>
    /// Список площадок не требует API-ключ и логин оператора. Выбор уже записанной площадки сохраняется.
    /// </summary>
    [Fact]
    public async Task List_сохраняет_галку_известной_площадки()
    {
        var gateway = new FakeVetisGateway
        {
            Enterprises =
            [
                new VetisEnterprise
                {
                    Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    Name = "Склад",
                    Address = "ул. Ленина, 12"
                },
                new VetisEnterprise
                {
                    Guid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                    Name = "Магазин",
                    Address = "ул. Кирова, 4"
                }
            ]
        };
        var organizations = new MemoryOrganizations();
        organizations.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            VetisSettings = new OrganizationVetisSettings
            {
                BasicPassword = "stored-secret",
                Enterprises =
                [
                    new OrganizationEnterprise
                    {
                        Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                        Name = "Склад",
                        Presentation = "Север",
                        Participates = true,
                        IncomingFrom = "2026-09-01"
                    }
                ]
            }
        };
        var sut = Create(gateway, organizations);
        var request = new VetisProbeRequest
        {
            BasicLogin = "user",
            BasicPassword = SecretMask.Placeholder,
            IssuerId = "issuer"
        };

        var result = await sut.ListEnterprisesAsync("o1", request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Enterprises.Count);
        Assert.Equal("Север", result.Value.Enterprises[0].Presentation);
        Assert.True(result.Value.Enterprises[0].Participates);
        Assert.Equal("ул. Кирова, 4", result.Value.Enterprises[1].Presentation);
        Assert.False(result.Value.Enterprises[1].Participates);
        Assert.Equal("", gateway.LastCall?.ApiKey);
    }

    /// <summary>
    /// Проверка соединения тоже обращение к API: оно видно во вкладке «Заявки».
    /// </summary>
    [Fact]
    public async Task Check_пишет_заявку_в_журнал()
    {
        var applications = new MemoryVetisApplications();
        var sut = Create(new FakeVetisGateway(), applications: applications);

        await sut.CheckAsync("o1", ReadyRequest(), CancellationToken.None);

        var application = Assert.Single(applications.Store.Values);
        Assert.Equal("GetBusinessEntityUser", application.Operation);
        Assert.Equal("o1", application.OrganizationId);
        Assert.Equal("", application.EnterpriseGuid);
    }

    [Fact]
    public async Task Check_с_неполными_настройками_не_обращается_к_API_и_не_пишет_заявку()
    {
        var applications = new MemoryVetisApplications();
        var sut = Create(new FakeVetisGateway(), applications: applications);
        var request = ReadyRequest();
        request.ApiKey = "";

        await sut.CheckAsync("o1", request, CancellationToken.None);

        Assert.Empty(applications.Store);
    }

    [Fact]
    public async Task List_пишет_заявку_на_каждую_страницу_площадок()
    {
        var applications = new MemoryVetisApplications();
        var sut = Create(new FakeVetisGateway(), applications: applications);
        var request = new VetisProbeRequest { BasicLogin = "user", BasicPassword = "typed", IssuerId = "issuer" };

        await sut.ListEnterprisesAsync("o1", request, CancellationToken.None);

        Assert.Equal(2, applications.Store.Count);
        Assert.All(applications.Store.Values, item => Assert.Equal("GetActivityLocationList", item.Operation));
    }

    private static VetisProbeRequest ReadyRequest() => new()
    {
        ApiKey = "key",
        BasicLogin = "user",
        BasicPassword = "typed",
        IssuerId = "issuer",
        InitiatorLogin = "operator"
    };

    private static VetisProbeService Create(
        FakeVetisGateway gateway,
        MemoryOrganizations? organizations = null,
        bool enable = true,
        MemoryVetisApplications? applications = null)
    {
        return new VetisProbeService(
            gateway,
            organizations ?? new MemoryOrganizations(),
            new MemoryParameters(enable),
            new VetisApplicationRecorder(
                applications ?? new MemoryVetisApplications(),
                NullLogger<VetisApplicationRecorder>.Instance),
            NullLogger<VetisProbeService>.Instance);
    }

    private sealed class MemoryParameters : IParametersService
    {
        private readonly Parameters _parameters = new();

        public MemoryParameters(bool enable)
        {
            _parameters.VetisSettings.Enable = enable;
        }

        public Task<Parameters> Current() => Task.FromResult(_parameters);

        public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
    }

    private sealed class MemoryOrganizations : IOrganizationRepository
    {
        public Dictionary<string, OrganizationEntity> Store { get; } = new();

        public Task<Result> Create(OrganizationEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(OrganizationEntity entity) => throw new NotImplementedException();

        public Task<Result<OrganizationEntity>> GetById(string id)
            => Task.FromResult(Store.TryGetValue(id, out var entity)
                ? Result.Success(entity)
                : Result.Failure<OrganizationEntity>("нет"));

        public Task<Result<OrganizationEntity>> GetByInn(string inn) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<OrganizationEntity>> All() => throw new NotImplementedException();
    }

    private sealed class FakeVetisGateway : IVetisGateway
    {
        public int Checks { get; private set; }

        public VetisCall? LastCall { get; private set; }

        public List<VetisEnterprise> Enterprises { get; init; } = [];

        public Task<VetisExchange> CheckConnectionAsync(VetisCall call, CancellationToken cancellationToken)
        {
            Checks++;
            LastCall = call;
            return Task.FromResult(new VetisExchange
            {
                Completed = true,
                Status = "COMPLETED",
                ApplicationId = "app-1",
                Operation = "GetBusinessEntityUser"
            });
        }

        public Task<VetisEnterpriseList> ListEnterprisesAsync(VetisCall call, CancellationToken cancellationToken)
        {
            LastCall = call;
            return Task.FromResult(new VetisEnterpriseList
            {
                Completed = true,
                Enterprises = Enterprises,
                Exchanges =
                [
                    new VetisExchange { Operation = "GetActivityLocationList", Completed = true, Status = "COMPLETED" },
                    new VetisExchange { Operation = "GetActivityLocationList", Completed = true, Status = "COMPLETED" }
                ]
            });
        }

        public Task<VetisIncomingResult> GetIncomingAsync(
            VetisCall call,
            VetisIncomingRequest request,
            CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
