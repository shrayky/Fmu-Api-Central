using CSharpFunctionalExtensions;
using Domain.Dto;
using Domain.Entitys;

namespace Domain.Authentication.Interfaces
{
    public interface IUserCredentialService
    {
        Task<Result<UserEntity>> GetUserByLogin(string login);
        Task<bool> ValidatePassword(UserEntity user, string password);
    }
}
