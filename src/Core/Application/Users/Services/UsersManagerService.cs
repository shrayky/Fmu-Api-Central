using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Dto.Responces;
using Domain.Entitys;
using Domain.Entitys.Users.Dto;
using Domain.Entitys.Users.Interfaces;

namespace Application.Users.Services;

[AutoRegisterService]
public class UsersManagerService : IUsersManagerService
{
    private readonly IUserRepository _repository;

    public UsersManagerService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Create(UserView data)
    {
        var validateResult = ValidateName(data.Name);
        if (validateResult.IsFailure)
            return validateResult;

        if (string.IsNullOrWhiteSpace(data.Password))
            return Result.Failure("Укажите пароль");

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

        var existing = await _repository.GetById(id);
        if (existing.IsFailure)
            return Result.Failure(existing.Error);

        existing.Value.Password = password;
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
            entity.Password = data.Password;

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

    private static UserEntity ToEntity(UserView data) => new()
    {
        Id = string.IsNullOrWhiteSpace(data.Id) ? Guid.NewGuid().ToString() : data.Id,
        Name = data.Name.Trim(),
        Password = data.Password
    };
}
