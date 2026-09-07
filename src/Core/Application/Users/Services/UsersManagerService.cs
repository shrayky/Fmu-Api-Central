using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication;
using Domain.Authentication.Interfaces;
using Domain.Dto.Responces;
using Domain.Entitys;
using Domain.Entitys.Users.Dto;
using Domain.Entitys.Users.Interfaces;

namespace Application.Users.Services;

[AutoRegisterService]
public class UsersManagerService : IUsersManagerService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;

    public UsersManagerService(IUserRepository repository, IPasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result> Create(UserView data)
    {
        var validateResult = ValidateName(data.Name);
        if (validateResult.IsFailure)
            return validateResult;

        if (string.IsNullOrWhiteSpace(data.Password))
            return Result.Failure("Укажите пароль");

        var passwordResult = ValidateNewPassword(data.Password);
        if (passwordResult.IsFailure)
            return passwordResult;

        var uniqueNameResult = await EnsureUniqueName(data.Id, data.Name);
        if (uniqueNameResult.IsFailure)
            return uniqueNameResult;

        var existing = await _repository.GetById(data.Id);
        if (existing.IsFailure)
            return await _repository.Create(ToEntity(data));

        return await ApplyChanges(existing.Value, data);
    }

    public async Task<Result> Update(UserView data)
    {
        var validateResult = ValidateName(data.Name);
        if (validateResult.IsFailure)
            return validateResult;

        var existing = await _repository.GetById(data.Id);
        if (existing.IsFailure)
            return Result.Failure(existing.Error);

        var uniqueNameResult = await EnsureUniqueName(data.Id, data.Name);
        if (uniqueNameResult.IsFailure)
            return uniqueNameResult;

        return await ApplyChanges(existing.Value, data);
    }

    public async Task<Result> ChangePassword(string id, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Result.Failure("Укажите пароль");

        var passwordResult = ValidateNewPassword(password);
        if (passwordResult.IsFailure)
            return passwordResult;

        var existing = await _repository.GetById(id);
        if (existing.IsFailure)
            return Result.Failure(existing.Error);

        existing.Value.Password = _passwordHasher.Hash(password.Trim());
        existing.Value.MustChangePassword = false;
        return await _repository.Update(existing.Value);
    }

    public async Task<Result> Delete(string id)
    {
        var existing = await _repository.GetById(id);
        if (existing.IsFailure)
            return Result.Failure(existing.Error);

        var users = await _repository.All();
        if (users.Count <= 1)
            return Result.Failure("Нельзя удалить последнего пользователя");

        return await _repository.Delete(id);
    }

    public async Task<PaginatedResponse<UserView>> List(int pageNumber, int pageSize)
    {
        var entityList = await _repository.List(pageNumber, pageSize);

        return new PaginatedResponse<UserView>
        {
            Description = entityList.Description,
            ListEnabled = entityList.ListEnabled,
            TotalCount = entityList.TotalCount,
            PageSize = entityList.PageSize,
            CurrentPage = entityList.CurrentPage,
            Content = entityList.Content.Select(entity => new UserView
            {
                Id = entity.Id,
                Name = entity.Name,
                Password = string.Empty,
                IsLastUser = entityList.TotalCount <= 1
            })
        };
    }

    private async Task<Result> ApplyChanges(UserEntity entity, UserView data)
    {
        entity.Name = data.Name.Trim();
        if (!string.IsNullOrWhiteSpace(data.Password))
        {
            var passwordResult = ValidateNewPassword(data.Password);
            if (passwordResult.IsFailure)
                return passwordResult;

            entity.Password = _passwordHasher.Hash(data.Password.Trim());
            entity.MustChangePassword = false;
        }

        return await _repository.Update(entity);
    }

    private async Task<Result> EnsureUniqueName(string id, string name)
    {
        var byName = await _repository.ByName(name.Trim());
        if (byName.IsSuccess && byName.Value.Id != id)
            return Result.Failure("Пользователь с таким именем уже существует");

        return Result.Success();
    }

    private static Result ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure("Укажите имя пользователя");

        return Result.Success();
    }

    private UserEntity ToEntity(UserView data) => new()
    {
        Id = string.IsNullOrWhiteSpace(data.Id) ? Guid.NewGuid().ToString() : data.Id,
        Name = data.Name.Trim(),
        Password = _passwordHasher.Hash(data.Password.Trim()),
        MustChangePassword = false
    };

    private static Result ValidateNewPassword(string password)
    {
        if (string.Equals(password.Trim(), DefaultUserCredentials.Password, StringComparison.Ordinal))
            return Result.Failure("Нельзя оставить пароль по умолчанию");

        return Result.Success();
    }
}
