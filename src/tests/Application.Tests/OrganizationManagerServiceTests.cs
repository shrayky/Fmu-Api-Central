using Application.Organization.Services;
using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Configuration;
using Domain.Dto.Responces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.Interfaces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Dto;
using Domain.Entitys.Organization.Interfaces;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;

namespace Application.Tests;

public class OrganizationManagerServiceTests
{
    /// <summary>
    /// Список отдаёт маску, сущность в репозитории не меняется.
    /// </summary>
    [Fact]
    public async Task List_маскирует_пароль_TrueApi()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                Enable = true,
                Password = "true-api-secret",
                DigitalSignature = "thumbprint"
            }
        };
        var sut = CreateSut(repository);

        var result = await sut.List(1, 50);

        var view = Assert.Single(result.Content);
        Assert.Equal(SecretMask.Placeholder, view.TrueApiIntegrationSettings.Password);
        Assert.Equal("true-api-secret", repository.Store["o1"].TrueApiIntegrationSettings.Password);
        Assert.Equal("thumbprint", view.TrueApiIntegrationSettings.DigitalSignature);
    }

    /// <summary>
    /// Удаление организации снимает её из списков групп.
    /// </summary>
    [Fact]
    public async Task Delete_убирает_организацию_из_групп()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity { Id = "o1", Name = "Орг", Inn = "1234567890" };
        var groups = new FakeGroupRepository();
        groups.Store["g1"] = new InstanceGroupEntity
        {
            Id = "g1",
            Name = "Группа",
            OrganizationIds = ["o1", "o2"]
        };
        var sut = CreateSut(repository, groups);

        var result = await sut.Delete("o1");

        Assert.True(result.IsSuccess);
        Assert.False(repository.Store.ContainsKey("o1"));
        Assert.Equal(["o2"], groups.Store["g1"].OrganizationIds);
    }

    /// <summary>
    /// POST с маской не затирает пароль True API.
    /// </summary>
    [Fact]
    public async Task Update_не_затирает_пароль_если_маска()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                Enable = true,
                Password = "true-api-secret"
            }
        };
        var sut = CreateSut(repository);

        var result = await sut.Update(new OrganizationView
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                Enable = true,
                Password = SecretMask.Placeholder
            }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("true-api-secret", repository.Store["o1"].TrueApiIntegrationSettings.Password);
    }

    /// <summary>
    /// Явная новая строка перезаписывает пароль True API.
    /// </summary>
    [Fact]
    public async Task Update_принимает_новый_пароль()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                Password = "true-api-secret"
            }
        };
        var sut = CreateSut(repository);

        var result = await sut.Update(new OrganizationView
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                Password = "new-true-api"
            }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("new-true-api", repository.Store["o1"].TrueApiIntegrationSettings.Password);
    }

    /// <summary>
    /// Срок кэша берётся из сессии Честного знака, а не из фиксированных 8 часов.
    /// </summary>
    [Fact]
    public async Task Token_сохраняет_uuid_и_срок_сессии()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "7700000000",
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings
            {
                Enable = true,
                Password = "pwd",
                DigitalSignature = "serial"
            }
        };
        var liveUntil = new DateTime(2026, 10, 10, 7, 0, 0, DateTimeKind.Local);
        var state = new RecordingApplicationState();
        var sut = new OrganizationManagerService(
            repository,
            new SessionTrueApiAuthService(new TrueApiSession("123e4567-e89b-12d3-a456-426655440000", liveUntil)),
            state,
            new FakeGroupRepository());

        var result = await sut.Token("7700000000");

        Assert.True(result.IsSuccess);
        Assert.Equal("123e4567-e89b-12d3-a456-426655440000", result.Value.Token);
        Assert.Equal(liveUntil, result.Value.Expired);
        Assert.Equal("123e4567-e89b-12d3-a456-426655440000", state.SavedToken);
        Assert.Equal(liveUntil, state.SavedUntil);
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        public Dictionary<string, OrganizationEntity> Store { get; } = new();

        public Task<Result> Create(OrganizationEntity entity)
        {
            Store[entity.Id] = entity;
            return Task.FromResult(Result.Success());
        }

        public Task<Result> Update(OrganizationEntity entity)
        {
            Store[entity.Id] = entity;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<OrganizationEntity>> GetById(string id)
        {
            if (!Store.TryGetValue(id, out var entity))
                return Task.FromResult(Result.Failure<OrganizationEntity>($"Не найдена организация с id {id}"));

            return Task.FromResult(Result.Success(entity));
        }

        public Task<Result<OrganizationEntity>> GetByInn(string inn)
        {
            var entity = Store.Values.FirstOrDefault(item => item.Inn == inn);
            return Task.FromResult(entity is null
                ? Result.Failure<OrganizationEntity>($"Не найдена организация с ИНН {inn}")
                : Result.Success(entity));
        }

        public Task<Result> Delete(string id)
        {
            Store.Remove(id);
            return Task.FromResult(Result.Success());
        }

        public Task<PaginatedResponse<OrganizationEntity>> List(int pageNumber, int pageSize)
        {
            var items = Store.Values.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PaginatedResponse<OrganizationEntity>
            {
                Content = items,
                TotalCount = Store.Count,
                CurrentPage = pageNumber,
                PageSize = pageSize
            });
        }

        public Task<List<OrganizationEntity>> All() => Task.FromResult(Store.Values.ToList());
    }

    private static OrganizationManagerService CreateSut(
        FakeOrganizationRepository repository,
        FakeGroupRepository? groups = null)
        => new(repository, new FakeTrueApiAuthService(), new FakeApplicationState(), groups ?? new FakeGroupRepository());

    private sealed class FakeGroupRepository : IInstanceGroupRepository
    {
        public Dictionary<string, InstanceGroupEntity> Store { get; } = new();

        public Task<Result> Create(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result> Update(InstanceGroupEntity entity) => throw new NotImplementedException();

        public Task<Result<InstanceGroupEntity>> GetById(string id) => throw new NotImplementedException();

        public Task<Result> Delete(string id) => throw new NotImplementedException();

        public Task<PaginatedResponse<InstanceGroupEntity>> List(int pageNumber, int pageSize)
            => throw new NotImplementedException();

        public Task<List<InstanceGroupEntity>> All() => Task.FromResult(Store.Values.ToList());

        public Task<List<InstanceGroupEntity>> ByListId(List<string> ids) => throw new NotImplementedException();

        public Task<Result> ClearSettingsSchemaLink(string settingsSchemaId)
            => throw new NotImplementedException();

        public Task<Result> ClearOrganizationLink(string organizationId)
        {
            foreach (var group in Store.Values)
                group.OrganizationIds.RemoveAll(id => id == organizationId);

            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeTrueApiAuthService : ITrueApiAuthService
    {
        public Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber)
            => Task.FromResult(Result.Failure<TrueApiSession>("не используется в тесте"));
    }

    private sealed class SessionTrueApiAuthService : ITrueApiAuthService
    {
        private readonly TrueApiSession _session;

        public SessionTrueApiAuthService(TrueApiSession session) => _session = session;

        public Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber)
            => Task.FromResult(Result.Success(_session));
    }

    private sealed class RecordingApplicationState : IApplicationState
    {
        public string SavedToken { get; private set; } = string.Empty;
        public DateTime SavedUntil { get; private set; }

        public void DbStateUpdate(bool isOnline) { }
        public bool DbState() => true;
        public void UpdateNeedRestart(bool need) { }
        public bool NeedRestart() => false;

        public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil)
        {
            SavedToken = token;
            SavedUntil = lifeUntil;
        }

        public TrueApiToken TrueApiToken(string inn) => new();
        public IReadOnlyList<TrueApiToken> TrueApiTokens() => [];
        public void MarkGisMtPushPending() { }
        public bool GisMtPushPending() => false;
        public void ClearGisMtPushPending() { }
    }

    private sealed class FakeApplicationState : IApplicationState
    {
        public void DbStateUpdate(bool isOnline) { }
        public bool DbState() => true;
        public void UpdateNeedRestart(bool need) { }
        public bool NeedRestart() => false;
        public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil) { }
        public TrueApiToken TrueApiToken(string inn) => new();
        public IReadOnlyList<TrueApiToken> TrueApiTokens() => [];
        public void MarkGisMtPushPending() { }
        public bool GisMtPushPending() => false;
        public void ClearGisMtPushPending() { }
    }
}
