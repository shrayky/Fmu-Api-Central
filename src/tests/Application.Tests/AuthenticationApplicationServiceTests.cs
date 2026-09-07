using Application.Authentication.Services;
using Authentication.Services;
using CSharpFunctionalExtensions;
using Domain.Authentication;
using Domain.Authentication.Dto;
using Domain.Authentication.Interfaces;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Entitys;
using Domain.Entitys.Users.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class AuthenticationApplicationServiceTests
{
    /// <summary>
    /// Неверный пароль учитывается; после лимита вход отклоняется без проверки пароля.
    /// </summary>
    [Fact]
    public async Task Authenticate_после_лимита_неудач_не_проверяет_пароль()
    {
        var auth = new FakeUserAuthenticationService { IsValid = false };
        var attempts = CreateAttemptService();
        var sut = CreateSut(auth, attempts);

        for (var i = 0; i < 5; i++)
            await sut.Authenticate("admin", "wrong");

        auth.IsValid = true;
        auth.ValidateCalls = 0;

        var result = await sut.Authenticate("admin", "admin");

        Assert.True(result.IsFailure);
        Assert.Equal("Неверный логин или пароль", result.Error);
        Assert.Equal(0, auth.ValidateCalls);
    }

    /// <summary>
    /// Успешный вход сбрасывает блокировку.
    /// </summary>
    [Fact]
    public async Task Authenticate_успех_сбрасывает_счётчик()
    {
        var auth = new FakeUserAuthenticationService { IsValid = false };
        var attempts = CreateAttemptService();
        var sut = CreateSut(auth, attempts);

        for (var i = 0; i < 4; i++)
            await sut.Authenticate("admin", "wrong");

        auth.IsValid = true;
        var success = await sut.Authenticate("admin", "admin");
        Assert.True(success.IsSuccess);

        auth.IsValid = false;
        for (var i = 0; i < 4; i++)
            await sut.Authenticate("admin", "wrong");

        Assert.False(attempts.IsLocked("admin"));
    }

    /// <summary>
    /// Пароль admin в БД — вход без JWT, нужна смена.
    /// </summary>
    [Fact]
    public async Task Authenticate_с_паролем_по_умолчанию_не_выдаёт_jwt()
    {
        var tokens = new FakeTokenService();
        var credentials = new FakeUserCredentialService
        {
            User = new UserEntity
            {
                Name = "admin",
                Password = DefaultUserCredentials.Password,
                MustChangePassword = true
            }
        };
        var sut = CreateSut(
            new FakeUserAuthenticationService { IsValid = true },
            CreateAttemptService(),
            credentials,
            tokens);

        var result = await sut.Authenticate("admin", "admin");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.MustChangePassword);
        Assert.Equal(string.Empty, result.Value.AccessToken);
        Assert.Equal(0, tokens.GenerateCalls);
    }

    /// <summary>
    /// Смена пароля сбрасывает флаг и не принимает admin.
    /// </summary>
    [Fact]
    public async Task ChangePassword_записывает_новый_пароль()
    {
        var user = new UserEntity
        {
            Id = "u1",
            Name = "admin",
            Password = "admin",
            MustChangePassword = true
        };
        var repository = new FakeAuthUserRepository();
        repository.Store[user.Id] = user;
        var parameters = new FakeParametersService();
        var sut = CreateSut(
            new FakeUserAuthenticationService { IsValid = true },
            CreateAttemptService(),
            new FakeUserCredentialService { User = user },
            repository: repository,
            parameters: parameters);

        var rejected = await sut.ChangePassword("admin", "admin", "admin");
        Assert.True(rejected.IsFailure);

        var changed = await sut.ChangePassword("admin", "admin", "secret1");
        Assert.True(changed.IsSuccess);
        Assert.Equal("h:secret1", repository.Store["u1"].Password);
        Assert.False(repository.Store["u1"].MustChangePassword);
        Assert.True(parameters.CurrentValue.Security.PasswordConfigured);
    }

    /// <summary>
    /// Без БД менять пароль некуда.
    /// </summary>
    [Fact]
    public async Task ChangePassword_в_fallback_отклоняет()
    {
        var sut = CreateSut(
            new FakeUserAuthenticationService { IsValid = true, FallbackActive = true },
            CreateAttemptService(),
            new FakeUserCredentialService { UserExists = false });

        var result = await sut.ChangePassword("admin", "admin", "secret1");

        Assert.True(result.IsFailure);
    }

    /// <summary>
    /// Пароль уже ставили — fallback не выдаёт JWT, даже если CouchDB недоступна.
    /// </summary>
    [Fact]
    public async Task Authenticate_при_passwordConfigured_в_fallback_не_пускает()
    {
        var tokens = new FakeTokenService();
        var sut = CreateSut(
            new FakeUserAuthenticationService { IsValid = true, FallbackActive = true },
            CreateAttemptService(),
            new FakeUserCredentialService { UserExists = false },
            tokens,
            parameters: new FakeParametersService { PasswordConfigured = true });

        var result = await sut.Authenticate("admin", "admin");

        Assert.True(result.IsFailure);
        Assert.Equal(0, tokens.GenerateCalls);
    }

    /// <summary>
    /// Первый запуск без БД — fallback admin/admin ещё пускает в настройки.
    /// </summary>
    [Fact]
    public async Task Authenticate_в_fallback_без_флага_выдаёт_jwt()
    {
        var tokens = new FakeTokenService();
        var sut = CreateSut(
            new FakeUserAuthenticationService { IsValid = true, FallbackActive = true },
            CreateAttemptService(),
            new FakeUserCredentialService { UserExists = false },
            tokens);

        var result = await sut.Authenticate("admin", "admin");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.MustChangePassword);
        Assert.Equal(1, tokens.GenerateCalls);
    }

    private static LoginAttemptService CreateAttemptService()
        => new(
            new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System,
            new LoginAttemptOptions());

    private static AuthenticationApplicationService CreateSut(
        IUserAuthenticationService authentication,
        ILoginAttemptService attempts,
        IUserCredentialService? credentials = null,
        ITokenService? tokens = null,
        IRefreshTokenService? refresh = null,
        IUserRepository? repository = null,
        IParametersService? parameters = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(authentication);
        services.AddSingleton(attempts);
        services.AddSingleton(credentials ?? new FakeUserCredentialService());
        services.AddSingleton(tokens ?? new FakeTokenService());
        services.AddSingleton(refresh ?? new FakeRefreshTokenService());
        services.AddSingleton(repository ?? new FakeAuthUserRepository());
        services.AddSingleton<IPasswordHasher>(new FakePasswordHasher());
        services.AddSingleton(parameters ?? new FakeParametersService());

        return new AuthenticationApplicationService(
            services.BuildServiceProvider(),
            NullLogger<AuthenticationApplicationService>.Instance);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "h:" + password;
        public bool IsHashed(string stored) => stored.StartsWith("h:");
        public bool Verify(string password, string stored)
            => IsHashed(stored) ? stored == "h:" + password : stored == password;
    }

    private sealed class FakeParametersService : IParametersService
    {
        public Parameters CurrentValue { get; } = new();

        public bool PasswordConfigured
        {
            get => CurrentValue.Security.PasswordConfigured;
            set => CurrentValue.Security.PasswordConfigured = value;
        }

        public Task<Parameters> Current() => Task.FromResult(CurrentValue);

        public Task<bool> Update(Parameters parameters)
        {
            CurrentValue.Security.PasswordConfigured = parameters.Security.PasswordConfigured;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeUserAuthenticationService : IUserAuthenticationService
    {
        public bool IsValid { get; set; }
        public bool FallbackActive { get; set; }
        public int ValidateCalls { get; set; }

        public Task<bool> ValidateCredentials(string login, string password)
        {
            ValidateCalls++;
            return Task.FromResult(IsValid);
        }

        public Task<bool> IsFallbackActive() => Task.FromResult(FallbackActive);
    }

    private sealed class FakeUserCredentialService : IUserCredentialService
    {
        public bool UserExists { get; set; } = true;
        public UserEntity? User { get; set; }

        public Task<Result<UserEntity>> GetUserByLogin(string login)
        {
            if (!UserExists)
                return Task.FromResult(Result.Failure<UserEntity>("не найден"));

            return Task.FromResult(Result.Success(User ?? new UserEntity { Name = login }));
        }

        public Task<bool> ValidatePassword(UserEntity user, string password) => Task.FromResult(true);
    }

    private sealed class FakeTokenService : ITokenService
    {
        public int GenerateCalls { get; private set; }

        public string GenerateToken(string login) => "access";

        public Result<TokenPair> GenerateTokenPair(string login)
        {
            GenerateCalls++;
            return Result.Success(new TokenPair("access", "refresh", DateTime.UtcNow.AddHours(1)));
        }

        public Result<TokenPair> RefreshAccessToken(string refreshToken)
            => Result.Success(new TokenPair("access", refreshToken, DateTime.UtcNow.AddHours(1)));

        public bool ValidateRefreshToken(string refreshToken) => true;
    }

    private sealed class FakeRefreshTokenService : IRefreshTokenService
    {
        public Result<string> LoginByRefreshToken(string refreshToken) => Result.Success("admin");
        public bool IsRefreshTokenValid(string refreshToken) => true;
        public void RemoveRefreshToken(string refreshToken) { }
        public void SaveRefreshToken(string refreshToken, string login, DateTime expiresAt) { }
    }

    private sealed class FakeAuthUserRepository : IUserRepository
    {
        public Dictionary<string, UserEntity> Store { get; } = new();

        public string DatabaseName() => "users";

        public Task<Result<UserEntity>> ByName(string login)
        {
            var user = Store.Values.FirstOrDefault(item => item.Name == login);
            return Task.FromResult(user is null
                ? Result.Failure<UserEntity>("не найден")
                : Result.Success(user));
        }

        public Task<Result<UserEntity>> GetById(string id)
            => Task.FromResult(Store.TryGetValue(id, out var user)
                ? Result.Success(user)
                : Result.Failure<UserEntity>("не найден"));

        public Task<Result> Create(UserEntity entity)
        {
            Store[entity.Id] = entity;
            return Task.FromResult(Result.Success());
        }

        public Task<Result> Update(UserEntity entity)
        {
            Store[entity.Id] = entity;
            return Task.FromResult(Result.Success());
        }

        public Task<Result> Delete(string id)
        {
            Store.Remove(id);
            return Task.FromResult(Result.Success());
        }

        public Task<Domain.Dto.Responces.PaginatedResponse<UserEntity>> List(int pageNumber, int pageSize)
            => Task.FromResult(new Domain.Dto.Responces.PaginatedResponse<UserEntity>
            {
                Content = Store.Values.ToList(),
                TotalCount = Store.Count,
                CurrentPage = pageNumber,
                PageSize = pageSize
            });

        public Task<List<UserEntity>> All() => Task.FromResult(Store.Values.ToList());
    }
}
