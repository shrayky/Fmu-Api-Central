using Authentication.Services;
using CSharpFunctionalExtensions;
using Domain.Authentication;
using Domain.Authentication.Interfaces;
using Domain.Database.Interfaces;
using Domain.Entitys;
using Domain.Entitys.Users.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class UserAuthenticationServiceTests
{
    /// <summary>
    /// Enable включён, но CouchDB не отвечает — fallback без запроса к users.
    /// </summary>
    [Fact]
    public async Task IsFallbackActive_без_подключения_не_читает_users()
    {
        var health = new FakeDbHealthService { Enabled = true, ConnectionHealthy = false };
        var records = new FakeRepositoryHealthService();
        var sut = CreateSut(health, records);

        Assert.True(await sut.IsFallbackActive());
        Assert.Equal(0, records.HasRecordsCalls);
    }

    /// <summary>
    /// Без подключения admin/admin принимается, к users не ходим.
    /// </summary>
    [Fact]
    public async Task ValidateCredentials_без_подключения_не_читает_пользователя()
    {
        var credentials = new FakeUserCredentials();
        var sut = CreateSut(
            new FakeDbHealthService { Enabled = true, ConnectionHealthy = false },
            new FakeRepositoryHealthService(),
            credentials);

        Assert.True(await sut.ValidateCredentials("admin", "admin"));
        Assert.Equal(0, credentials.GetUserCalls);
    }

    /// <summary>
    /// БД выключена — fallback без проверки users.
    /// </summary>
    [Fact]
    public async Task IsFallbackActive_при_выключенной_бд_не_читает_users()
    {
        var records = new FakeRepositoryHealthService();
        var sut = CreateSut(new FakeDbHealthService { Enabled = false }, records);

        Assert.True(await sut.IsFallbackActive());
        Assert.Equal(0, records.HasRecordsCalls);
    }

    /// <summary>
    /// Подключение есть, users пуст — fallback.
    /// </summary>
    [Fact]
    public async Task IsFallbackActive_при_пустых_users_включает_fallback()
    {
        var records = new FakeRepositoryHealthService { RecordsExist = false };
        var sut = CreateSut(
            new FakeDbHealthService { Enabled = true, ConnectionHealthy = true },
            records);

        Assert.True(await sut.IsFallbackActive());
        Assert.Equal(1, records.HasRecordsCalls);
    }

    /// <summary>
    /// Подключение есть и users не пуст — пароль из БД.
    /// </summary>
    [Fact]
    public async Task ValidateCredentials_при_живой_бд_читает_пользователя()
    {
        var credentials = new FakeUserCredentials();
        var sut = CreateSut(
            new FakeDbHealthService { Enabled = true, ConnectionHealthy = true },
            new FakeRepositoryHealthService { RecordsExist = true },
            credentials);

        Assert.False(await sut.ValidateCredentials("admin", "admin"));
        Assert.Equal(1, credentials.GetUserCalls);
    }

    private static UserAuthenticationService CreateSut(
        IDbHealthService health,
        IRepositoryHealthService records,
        IUserCredentialService? credentials = null,
        IAuthenticationPolicyService? policy = null,
        IUserRepository? users = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(health);
        services.AddSingleton(records);
        services.AddSingleton(credentials ?? new FakeUserCredentials());
        services.AddSingleton(policy ?? new FakeAuthPolicy());

        return new UserAuthenticationService(
            services.BuildServiceProvider(),
            NullLogger<UserAuthenticationService>.Instance,
            users ?? new FakeUsers());
    }

    private sealed class FakeDbHealthService : IDbHealthService
    {
        public bool Enabled { get; set; }
        public bool ConnectionHealthy { get; set; }

        public Task<bool> IsDatabaseEnabled() => Task.FromResult(Enabled);
        public Task<bool> IsDatabaseAccessible(string databaseName) => Task.FromResult(ConnectionHealthy);
        public Task<Dictionary<string, bool>> GetAllDatabasesStatus() => Task.FromResult(new Dictionary<string, bool>());
        public Task<bool> IsConnectionHealthy() => Task.FromResult(ConnectionHealthy);
    }

    private sealed class FakeRepositoryHealthService : IRepositoryHealthService
    {
        public int HasRecordsCalls { get; private set; }
        public bool RecordsExist { get; set; }

        public Task<bool> HasRecords(string repositoryName)
        {
            HasRecordsCalls++;
            return Task.FromResult(RecordsExist);
        }

        public Task<int> GetRecordCount(string repositoryName) => Task.FromResult(RecordsExist ? 1 : 0);
        public Task<bool> IsRepositoryHealthy(string repositoryName) => Task.FromResult(true);
        public Task<Dictionary<string, int>> GetAllRepositoriesRecordCount() => Task.FromResult(new Dictionary<string, int>());
    }

    private sealed class FakeUserCredentials : IUserCredentialService
    {
        public int GetUserCalls { get; private set; }

        public Task<Result<UserEntity>> GetUserByLogin(string login)
        {
            GetUserCalls++;
            return Task.FromResult(Result.Failure<UserEntity>("не должен вызываться"));
        }

        public Task<bool> ValidatePassword(UserEntity user, string password) => Task.FromResult(true);
    }

    private sealed class FakeAuthPolicy : IAuthenticationPolicyService
    {
        public Task<bool> ValidateFallbackCredentials(string login, string password)
            => Task.FromResult(login == DefaultUserCredentials.Login && password == DefaultUserCredentials.Password);

        public bool IsFallbackModeEnabled() => true;
    }

    private sealed class FakeUsers : IUserRepository
    {
        public string DatabaseName() => "fmu-api-central-users";
        public Task<Result<UserEntity>> ByName(string login) => Task.FromResult(Result.Failure<UserEntity>("нет"));
        public Task<Result<UserEntity>> GetById(string id) => Task.FromResult(Result.Failure<UserEntity>("нет"));
        public Task<Result> Create(UserEntity entity) => Task.FromResult(Result.Success());
        public Task<Result> Update(UserEntity entity) => Task.FromResult(Result.Success());
        public Task<Result> Delete(string id) => Task.FromResult(Result.Success());
        public Task<Domain.Dto.Responces.PaginatedResponse<UserEntity>> List(int pageNumber, int pageSize)
            => Task.FromResult(new Domain.Dto.Responces.PaginatedResponse<UserEntity>
            {
                Content = [],
                TotalCount = 0,
                CurrentPage = pageNumber,
                PageSize = pageSize
            });
        public Task<List<UserEntity>> All() => Task.FromResult(new List<UserEntity>());
    }
}
