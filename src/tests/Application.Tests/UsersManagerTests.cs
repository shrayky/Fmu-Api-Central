using Application.Users.Services;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys;
using Domain.Entitys.Users.Dto;
using Domain.Entitys.Users.Interfaces;

namespace Application.Tests;

public class UsersManagerTests
{
    /// <summary>
    /// Список не возвращает пароли пользователей.
    /// </summary>
    [Fact]
    public async Task List_не_возвращает_пароли()
    {
        var repository = new FakeUserRepository();
        repository.Store["u1"] = new UserEntity
        {
            Id = "u1",
            Name = "admin",
            Password = "secret"
        };
        var sut = new UsersManagerService(repository);

        var result = await sut.List(1, 50);

        var user = Assert.Single(result.Content);
        Assert.Equal("admin", user.Name);
        Assert.Equal(string.Empty, user.Password);
        Assert.True(user.IsLastUser);
    }

    /// <summary>
    /// Два пользователя не помечаются как последние.
    /// </summary>
    [Fact]
    public async Task List_не_помечает_если_пользователей_двое()
    {
        var repository = new FakeUserRepository();
        repository.Store["u1"] = new UserEntity { Id = "u1", Name = "a1", Password = "p" };
        repository.Store["u2"] = new UserEntity { Id = "u2", Name = "a2", Password = "p" };
        var sut = new UsersManagerService(repository);

        var result = await sut.List(1, 50);

        Assert.All(result.Content, user => Assert.False(user.IsLastUser));
    }

    /// <summary>
    /// Создание отклоняется, если имя уже занято.
    /// </summary>
    [Fact]
    public async Task Create_отклоняет_дубликат_имени()
    {
        var repository = new FakeUserRepository();
        repository.Store["u1"] = new UserEntity { Id = "u1", Name = "admin", Password = "p" };
        var sut = new UsersManagerService(repository);

        var result = await sut.Create(new UserView
        {
            Id = "u2",
            Name = "admin",
            Password = "q"
        });

        Assert.True(result.IsFailure);
        Assert.Contains("уже существует", result.Error);
    }

    /// <summary>
    /// Создание без пароля отклоняется.
    /// </summary>
    [Fact]
    public async Task Create_требует_пароль()
    {
        var sut = new UsersManagerService(new FakeUserRepository());

        var result = await sut.Create(new UserView
        {
            Id = "u1",
            Name = "user",
            Password = ""
        });

        Assert.True(result.IsFailure);
        Assert.Contains("пароль", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создание без имени отклоняется.
    /// </summary>
    [Fact]
    public async Task Create_требует_имя()
    {
        var sut = new UsersManagerService(new FakeUserRepository());

        var result = await sut.Create(new UserView
        {
            Id = "u1",
            Name = "  ",
            Password = "secret"
        });

        Assert.True(result.IsFailure);
        Assert.Contains("имя", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создание сохраняет имя и пароль.
    /// </summary>
    [Fact]
    public async Task Create_сохраняет_пользователя()
    {
        var repository = new FakeUserRepository();
        var sut = new UsersManagerService(repository);

        var result = await sut.Create(new UserView
        {
            Id = "u1",
            Name = " user ",
            Password = "secret"
        });

        Assert.True(result.IsSuccess);
        var saved = repository.Store["u1"];
        Assert.Equal("user", saved.Name);
        Assert.Equal("secret", saved.Password);
    }

    /// <summary>
    /// Пустой пароль при изменении оставляет прежний.
    /// </summary>
    [Fact]
    public async Task Update_пустой_пароль_не_меняет_существующий()
    {
        var repository = new FakeUserRepository();
        repository.Store["u1"] = new UserEntity
        {
            Id = "u1",
            Name = "admin",
            Password = "old"
        };
        var sut = new UsersManagerService(repository);

        var result = await sut.Update(new UserView
        {
            Id = "u1",
            Name = "admin",
            Password = ""
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("old", repository.Store["u1"].Password);
    }

    /// <summary>
    /// Нельзя удалить последнего пользователя.
    /// </summary>
    [Fact]
    public async Task Delete_последнего_пользователя_запрещён()
    {
        var repository = new FakeUserRepository();
        repository.Store["u1"] = new UserEntity
        {
            Id = "u1",
            Name = "admin",
            Password = "p"
        };
        var sut = new UsersManagerService(repository);

        var result = await sut.Delete("u1");

        Assert.True(result.IsFailure);
        Assert.Contains("последнего пользователя", result.Error);
    }

    /// <summary>
    /// Удаление разрешено, если пользователей больше одного.
    /// </summary>
    [Fact]
    public async Task Delete_не_последнего_разрешён()
    {
        var repository = new FakeUserRepository();
        repository.Store["u1"] = new UserEntity { Id = "u1", Name = "admin", Password = "p" };
        repository.Store["u2"] = new UserEntity { Id = "u2", Name = "user", Password = "p" };
        var sut = new UsersManagerService(repository);

        var result = await sut.Delete("u2");

        Assert.True(result.IsSuccess);
        Assert.False(repository.Store.ContainsKey("u2"));
    }

    /// <summary>
    /// Смена пароля не трогает имя.
    /// </summary>
    [Fact]
    public async Task ChangePassword_меняет_только_пароль()
    {
        var repository = new FakeUserRepository();
        repository.Store["u1"] = new UserEntity
        {
            Id = "u1",
            Name = "admin",
            Password = "old"
        };
        var sut = new UsersManagerService(repository);

        var result = await sut.ChangePassword("u1", "secret1");

        Assert.True(result.IsSuccess);
        Assert.Equal("secret1", repository.Store["u1"].Password);
        Assert.Equal("admin", repository.Store["u1"].Name);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Dictionary<string, UserEntity> Store { get; } = new();

        public string DatabaseName() => "users";

        public Task<Result<UserEntity>> ByName(string login)
        {
            var user = Store.Values.FirstOrDefault(item => item.Name == login);
            return Task.FromResult(user is null
                ? Result.Failure<UserEntity>($"Не найден пользователь с именем {login}")
                : Result.Success(user));
        }

        public Task<Result<UserEntity>> GetById(string id)
        {
            if (!Store.TryGetValue(id, out var user))
                return Task.FromResult(Result.Failure<UserEntity>($"Не найден пользователь с id {id}"));

            return Task.FromResult(Result.Success(user));
        }

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

        public Task<PaginatedResponse<UserEntity>> List(int pageNumber, int pageSize)
        {
            var items = Store.Values.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PaginatedResponse<UserEntity>
            {
                Content = items,
                TotalCount = Store.Count,
                CurrentPage = pageNumber,
                PageSize = pageSize
            });
        }

        public Task<List<UserEntity>> All() => Task.FromResult(Store.Values.ToList());
    }
}
