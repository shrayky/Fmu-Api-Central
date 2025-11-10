using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Authentication.Interfaces;
using Domain.Entitys;
using Domain.Entitys.Users.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Authentication.Services
{
    [AutoRegisterService(ServiceLifetime.Scoped)]
    public class UserCredentialService : IUserCredentialService
    {
        private readonly IUserRepository _usersRepository;

        public UserCredentialService(IUserRepository usersRepository)
        {
            _usersRepository = usersRepository;
        }

        public async Task<Result<UserEntity>> GetUserByLogin(string login)
        {
            return await _usersRepository.ByName(login);
        }

        public async Task<bool> ValidatePassword(UserEntity user, string password)
        {
            // функция асинхронная потому что в будующем возможна более долгая и сложная валидация

            // что бы компиляторо не ругался
            await Task.Delay(1);
            
            return user.Password == password;
        }
    }
}
