using CSharpFunctionalExtensions;

namespace Domain.Entitys.Users.Interfaces;

public interface IUserRepository
{
    string DatabaseName();
    Task<Result<UserEntity>> ByName(string login);
}