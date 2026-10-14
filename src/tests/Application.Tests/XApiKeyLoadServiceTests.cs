using Application.Organization.Services;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Dto.Responces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class XApiKeyLoadServiceTests
{
    /// <summary>
    /// Без выбранного сертификата запрос в CDN не уходит.
    /// </summary>
    [Fact]
    public async Task Refresh_без_эцп_не_запрашивает_ключ()
    {
        var auth = new RecordingAuthService(new TrueApiSession("cdn-key", DateTime.Now.AddHours(1)));
        var repository = new MemoryOrganizationRepository();
        repository.Store["o1"] = Org("o1", "7700000000", "");
        var state = new MemoryApplicationState();
        var sut = new XApiKeyLoadService(auth, state, repository, NullLogger<XApiKeyLoadService>.Instance);

        await sut.Refresh(repository.Store.Values, CancellationToken.None);

        Assert.Equal(0, auth.Calls);
        Assert.Null(repository.Store["o1"].XApiKeyExpiresAt);
    }

    /// <summary>
    /// Живой ключ в памяти не перезапрашиваем.
    /// </summary>
    [Fact]
    public async Task Refresh_живой_ключ_не_запрашивает_новый()
    {
        var auth = new RecordingAuthService(new TrueApiSession("cdn-key", DateTime.Now.AddHours(1)));
        var repository = new MemoryOrganizationRepository();
        repository.Store["o1"] = Org("o1", "7700000000", "serial");
        var state = new MemoryApplicationState();
        var until = DateTime.Now.AddHours(2);
        state.UpdateXApiKey("7700000000", "already", until);
        var sut = new XApiKeyLoadService(auth, state, repository, NullLogger<XApiKeyLoadService>.Instance);

        await sut.Refresh(repository.Store.Values, CancellationToken.None);

        Assert.Equal(0, auth.Calls);
        Assert.Equal("already", state.XApiKey("7700000000").Token);
    }

    /// <summary>
    /// Успешный ответ кладёт ключ в память, а срок — в перечитанный документ, не затирая имя.
    /// </summary>
    [Fact]
    public async Task Refresh_пишет_ключ_в_память_и_срок_в_организацию()
    {
        var until = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var auth = new RecordingAuthService(new TrueApiSession("cdn-key", until));
        var repository = new MemoryOrganizationRepository();
        repository.Store["o1"] = Org("o1", "7700000000", "serial");
        repository.Store["o1"].Name = "Старое";
        repository.OnGet = () =>
        {
            var fresh = Org("o1", "7700000000", "serial");
            fresh.Name = "Новое";
            return fresh;
        };
        var state = new MemoryApplicationState();
        var sut = new XApiKeyLoadService(auth, state, repository, NullLogger<XApiKeyLoadService>.Instance);

        await sut.Refresh([Org("o1", "7700000000", "serial")], CancellationToken.None);

        Assert.Equal(1, auth.Calls);
        Assert.Equal("7700000000", auth.Inn);
        Assert.Equal("pwd", auth.Password);
        Assert.Equal("serial", auth.Signature);
        Assert.Equal("cdn-key", state.XApiKey("7700000000").Token);
        Assert.Equal(until, state.XApiKey("7700000000").LiveUntil);
        Assert.Equal(until, repository.Store["o1"].XApiKeyExpiresAt);
        Assert.Equal("Новое", repository.Store["o1"].Name);
    }

    /// <summary>
    /// Отказ CDN не записывает срок.
    /// </summary>
    [Fact]
    public async Task Refresh_ошибка_запроса_не_пишет_срок()
    {
        var auth = new RecordingAuthService(null);
        var repository = new MemoryOrganizationRepository();
        repository.Store["o1"] = Org("o1", "7700000000", "serial");
        var state = new MemoryApplicationState();
        var sut = new XApiKeyLoadService(auth, state, repository, NullLogger<XApiKeyLoadService>.Instance);

        await sut.Refresh(repository.Store.Values, CancellationToken.None);

        Assert.Equal(1, auth.Calls);
        Assert.Equal(string.Empty, state.XApiKey("7700000000").Token);
        Assert.Null(repository.Store["o1"].XApiKeyExpiresAt);
    }

    private static OrganizationEntity Org(string id, string inn, string signature)
        => new()
        {
            Id = id,
            Name = "Орг",
            Inn = inn,
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                Password = "pwd",
                DigitalSignature = signature
            }
        };

    private sealed class RecordingAuthService : ITrueApiAuthService
    {
        private readonly TrueApiSession? _session;

        public RecordingAuthService(TrueApiSession? session) => _session = session;

        public int Calls { get; private set; }
        public string Inn { get; private set; } = string.Empty;
        public string Password { get; private set; } = string.Empty;
        public string Signature { get; private set; } = string.Empty;

        public Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber)
            => Task.FromResult(Result.Failure<TrueApiSession>("не используется в тесте"));

        public Task<Result<TrueApiSession>> GenerateXApiKey(string inn, string password, string signatureNumber)
        {
            Calls++;
            Inn = inn;
            Password = password;
            Signature = signatureNumber;
            return Task.FromResult(_session == null
                ? Result.Failure<TrueApiSession>("cdn отказал")
                : Result.Success(_session));
        }
    }

    private sealed class MemoryOrganizationRepository : IOrganizationRepository
    {
        public Dictionary<string, OrganizationEntity> Store { get; } = new();
        public Func<OrganizationEntity>? OnGet { get; set; }

        public Task<Result> Create(OrganizationEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(OrganizationEntity entity)
        {
            Store[entity.Id] = entity;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<OrganizationEntity>> GetById(string id)
        {
            if (OnGet != null)
                return Task.FromResult(Result.Success(OnGet()));

            return Store.TryGetValue(id, out var entity)
                ? Task.FromResult(Result.Success(entity))
                : Task.FromResult(Result.Failure<OrganizationEntity>("нет"));
        }

        public Task<Result<OrganizationEntity>> GetByInn(string inn) => throw new NotImplementedException();
        public Task<Result> Delete(string id) => throw new NotImplementedException();
        public Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize) => throw new NotImplementedException();
        public Task<List<OrganizationEntity>> All() => Task.FromResult(Store.Values.ToList());
    }

    private sealed class MemoryApplicationState : IApplicationState
    {
        private readonly Dictionary<string, TrueApiToken> _keys = new();

        public void DbStateUpdate(bool isOnline) { }
        public bool DbState() => true;
        public void UpdateNeedRestart(bool need) { }
        public bool NeedRestart() => false;
        public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil) { }
        public TrueApiToken TrueApiToken(string inn) => new();
        public IReadOnlyList<TrueApiToken> TrueApiTokens() => [];

        public void UpdateXApiKey(string inn, string key, DateTime lifeUntil)
        {
            _keys[inn] = new TrueApiToken { Inn = inn, Token = key, LiveUntil = lifeUntil };
        }

        public TrueApiToken XApiKey(string inn)
            => _keys.TryGetValue(inn, out var key) && key.LiveUntil >= DateTime.Now ? key : new TrueApiToken();

        public IReadOnlyList<TrueApiToken> XApiKeys() => _keys.Values.ToList();
        public void MarkGisMtPushPending() { }
        public bool GisMtPushPending() => false;
        public void ClearGisMtPushPending() { }
    }
}
