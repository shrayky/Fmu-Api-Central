using System.Text.Json;
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
    /// Список отдаёт маску пароля и API-ключа Ветис, в документе оба значения остаются.
    /// </summary>
    [Fact]
    public async Task List_маскирует_пароль_Ветис()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            VetisSettings = new OrganizationVetisSettings
            {
                Enable = true,
                BasicPassword = "vetis-secret",
                ApiKey = "key"
            }
        };
        var sut = CreateSut(repository);

        var result = await sut.List(1, 50);

        var view = Assert.Single(result.Content);
        var vetis = view.VetisSettings;
        Assert.NotNull(vetis);
        Assert.Equal(SecretMask.Placeholder, vetis.BasicPassword);
        Assert.Equal(SecretMask.Placeholder, vetis.ApiKey);
        Assert.True(vetis.Enable);
        Assert.Equal("vetis-secret", repository.Store["o1"].VetisSettings.BasicPassword);
        Assert.Equal("key", repository.Store["o1"].VetisSettings.ApiKey);
    }

    /// <summary>
    /// Маска и пустой пароль Ветис не затирают сохранённый. Новый API-ключ записывается.
    /// </summary>
    [Fact]
    public async Task Update_не_затирает_пароль_Ветис()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            VetisSettings = new OrganizationVetisSettings { BasicPassword = "vetis-secret", ApiKey = "old" }
        };
        var sut = CreateSut(repository);

        var result = await sut.Update(new OrganizationView
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            TrueApiIntegrationSettings = new TrueApiIntegrationSettings(),
            VetisSettings = new OrganizationVetisSettings
            {
                BasicPassword = SecretMask.Placeholder,
                ApiKey = "new-key"
            }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("vetis-secret", repository.Store["o1"].VetisSettings.BasicPassword);
        Assert.Equal("new-key", repository.Store["o1"].VetisSettings.ApiKey);
    }

    /// <summary>
    /// Сохранение без вкладки Ветис не присылает vetisSettings. Пустой объект из разбора не должен стирать реквизиты.
    /// </summary>
    [Fact]
    public async Task Update_без_ветис_в_json_не_стирает_сохранённое()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            VetisSettings = new OrganizationVetisSettings
            {
                Enable = true,
                ApiKey = "key",
                BasicLogin = "user",
                BasicPassword = "vetis-secret",
                IssuerId = "issuer",
                InitiatorLogin = "operator",
                IncomingFrom = "2026-01-01",
                Enterprises =
                [
                    new OrganizationEnterprise
                    {
                        Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                        Name = "Склад",
                        Participates = true
                    }
                ]
            }
        };
        var sut = CreateSut(repository);
        var json = """{"id":"o1","name":"Новое имя","inn":"1234567890","trueApiIntegrationSettings":{"enable":false}}""";
        var data = JsonSerializer.Deserialize<OrganizationView>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var result = await sut.Update(data!);

        Assert.True(result.IsSuccess);
        Assert.Equal("Новое имя", repository.Store["o1"].Name);
        var saved = repository.Store["o1"].VetisSettings;
        Assert.True(saved.Enable);
        Assert.Equal("key", saved.ApiKey);
        Assert.Equal("user", saved.BasicLogin);
        Assert.Equal("vetis-secret", saved.BasicPassword);
        Assert.Equal("issuer", saved.IssuerId);
        Assert.Equal("operator", saved.InitiatorLogin);
        Assert.Equal("2026-01-01", saved.IncomingFrom);
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", Assert.Single(saved.Enterprises).Guid);
    }

    [Fact]
    public async Task Update_не_пишет_организацию_с_нулевой_задержкой_автогашения()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            VetisSettings = new OrganizationVetisSettings { AutoQuenchDelayHours = 2 }
        };
        var sut = CreateSut(repository);

        var result = await sut.Update(new OrganizationView
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            VetisSettings = new OrganizationVetisSettings { AutoQuench = true, AutoQuenchDelayHours = 0 }
        });

        Assert.True(result.IsFailure);
        Assert.Contains("больше нуля", result.Error);
        Assert.False(repository.Store["o1"].VetisSettings.AutoQuench);
        Assert.Equal(2, repository.Store["o1"].VetisSettings.AutoQuenchDelayHours);
    }

    [Fact]
    public async Task Update_сохраняет_автогашение_организации()
    {
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890"
        };
        var sut = CreateSut(repository);

        var result = await sut.Update(new OrganizationView
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            VetisSettings = new OrganizationVetisSettings { AutoQuench = true, AutoQuenchDelayHours = 6 }
        });

        Assert.True(result.IsSuccess);
        Assert.True(repository.Store["o1"].VetisSettings.AutoQuench);
        Assert.Equal(6, repository.Store["o1"].VetisSettings.AutoQuenchDelayHours);
        Assert.True((await sut.List(1, 50)).Content.Single().VetisSettings!.AutoQuench);
        Assert.Equal(6, (await sut.List(1, 50)).Content.Single().VetisSettings!.AutoQuenchDelayHours);
    }

    /// <summary>
    /// Срок x-api-key хранится в документе организации и виден в списке.
    /// </summary>
    [Fact]
    public async Task List_отдаёт_срок_x_api_key()
    {
        var until = new DateTime(2026, 10, 14, 18, 0, 0, DateTimeKind.Local);
        var repository = new FakeOrganizationRepository();
        repository.Store["o1"] = new OrganizationEntity
        {
            Id = "o1",
            Name = "Орг",
            Inn = "1234567890",
            XApiKeyExpiresAt = until
        };
        var sut = CreateSut(repository);

        var result = await sut.List(1, 50);

        var view = Assert.Single(result.Content);
        Assert.Equal(until, view.XApiKeyExpiresAt);
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

        public Task<Result> SaveCheckerSettings(string groupId, byte[] content)
            => throw new NotImplementedException();

        public Task<Result> DeleteCheckerSettings(string groupId)
            => throw new NotImplementedException();

        public Task<Result<CheckerSettingsRead>> ReadCheckerSettings(string groupId)
            => throw new NotImplementedException();

        public Task<Result<bool>> HasCheckerSettings(string groupId)
            => throw new NotImplementedException();
    }

    private sealed class FakeTrueApiAuthService : ITrueApiAuthService
    {
        public Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber)
            => Task.FromResult(Result.Failure<TrueApiSession>("не используется в тесте"));

        public Task<Result<TrueApiSession>> GenerateXApiKey(string inn, string password, string signatureNumber)
            => Task.FromResult(Result.Failure<TrueApiSession>("не используется в тесте"));
    }

    private sealed class SessionTrueApiAuthService : ITrueApiAuthService
    {
        private readonly TrueApiSession _session;

        public SessionTrueApiAuthService(TrueApiSession session) => _session = session;

        public Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber)
            => Task.FromResult(Result.Success(_session));

        public Task<Result<TrueApiSession>> GenerateXApiKey(string inn, string password, string signatureNumber)
            => Task.FromResult(Result.Failure<TrueApiSession>("не используется в тесте"));
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
        public void UpdateXApiKey(string inn, string key, DateTime lifeUntil) { }
        public TrueApiToken XApiKey(string inn) => new();
        public IReadOnlyList<TrueApiToken> XApiKeys() => [];
        public void PostponeXApiKey(string inn, DateTime nextAttempt) { }
        public DateTime? XApiKeyPostponedUntil(string inn) => null;
        public void ClearXApiKeyPostpone(string inn) { }
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
        public void UpdateXApiKey(string inn, string key, DateTime lifeUntil) { }
        public TrueApiToken XApiKey(string inn) => new();
        public IReadOnlyList<TrueApiToken> XApiKeys() => [];
        public void PostponeXApiKey(string inn, DateTime nextAttempt) { }
        public DateTime? XApiKeyPostponedUntil(string inn) => null;
        public void ClearXApiKeyPostpone(string inn) { }
        public void MarkGisMtPushPending() { }
        public bool GisMtPushPending() => false;
        public void ClearGisMtPushPending() { }
    }
}
